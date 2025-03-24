from pathlib import Path
import cv2
import depthai as dai
import numpy as np
import time
import argparse
import json
import blobconverter
import zmq
import pickle
import os
from datetime import datetime

# 지정된 front, rear mxid
front_mxid = "19443010C180962E00"
rear_mxid  = "19443010C142962E00"

# Change the current working directory to the script's directory
script_dir = os.path.dirname(os.path.abspath(__file__))
os.chdir(script_dir)

# parse arguments
parser = argparse.ArgumentParser()
parser.add_argument("-m", "--model", help="Provide model name or model path for inference",
                    default='yolov4_tiny_coco_416x416', type=str)
parser.add_argument("-c", "--config", help="Provide config path for inference",
                    default='./json/yolov4-tiny.json', type=str)
args = parser.parse_args()

# parse config
configPath = Path(args.config)
if not configPath.exists():
    raise ValueError("Path {} does not exist!".format(configPath))

with configPath.open() as f:
    config = json.load(f)
nnConfig = config.get("nn_config", {})

# parse input shape
if "input_size" in nnConfig:
    W, H = tuple(map(int, nnConfig.get("input_size").split('x')))

# extract metadata
metadata = nnConfig.get("NN_specific_metadata", {})
classes = metadata.get("classes", {})
coordinates = metadata.get("coordinates", {})
anchors = metadata.get("anchors", {})
anchorMasks = metadata.get("anchor_masks", {})
iouThreshold = metadata.get("iou_threshold", {})
confidenceThreshold = metadata.get("confidence_threshold", {})

print(metadata)

# parse labels
nnMappings = config.get("mappings", {})
labels = nnMappings.get("labels", {})

# get model path
nnPath = args.model
if not Path(nnPath).exists():
    print("No blob found at {}. Looking into DepthAI model zoo.".format(nnPath))
    nnPath = str(blobconverter.from_zoo(args.model, shaves=6, zoo_type="depthai", use_cache=True))
# sync outputs
syncNN = True

# Create pipeline
pipeline = dai.Pipeline()

# Define sources and outputs
camRgb = pipeline.create(dai.node.ColorCamera)
detectionNetwork = pipeline.create(dai.node.YoloDetectionNetwork)
xoutRgb = pipeline.create(dai.node.XLinkOut)
nnOut = pipeline.create(dai.node.XLinkOut)
monoLeft = pipeline.create(dai.node.MonoCamera)
monoRight = pipeline.create(dai.node.MonoCamera)
stereo = pipeline.create(dai.node.StereoDepth)
xoutDepth = pipeline.create(dai.node.XLinkOut)
xoutLeft = pipeline.create(dai.node.XLinkOut)
xoutRight = pipeline.create(dai.node.XLinkOut)
xoutVideo = pipeline.create(dai.node.XLinkOut)

xoutRgb.setStreamName("rgb")
nnOut.setStreamName("nn")
xoutDepth.setStreamName("depth")
xoutLeft.setStreamName("left")
xoutRight.setStreamName("right")
xoutVideo.setStreamName("video")

# RGB Camera
camRgb.setPreviewSize(W, H)
camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_720_P)
camRgb.setInterleaved(False)
camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
camRgb.setFps(10)

# Left and Right mono cameras
monoLeft.setBoardSocket(dai.CameraBoardSocket.CAM_B)
monoLeft.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
monoLeft.setFps(10)
monoRight.setBoardSocket(dai.CameraBoardSocket.CAM_C)
monoRight.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
monoRight.setFps(10)

# StereoDepth settings
stereo.setDefaultProfilePreset(dai.node.StereoDepth.PresetMode.ROBOTICS)
stereo.initialConfig.setConfidenceThreshold(200)
stereo.setLeftRightCheck(True)  # Enable left-right check
stereo.setExtendedDisparity(False)  # Disable extended disparity
stereo.setSubpixel(False)  # Disable subpixel
stereo.setDepthAlign(dai.CameraBoardSocket.CAM_A)  # Align depth map to the RGB camera
stereo.setOutputSize(1280, 720)  # Output size for depth map

# Network specific settings
detectionNetwork.setConfidenceThreshold(confidenceThreshold)
detectionNetwork.setNumClasses(classes)
detectionNetwork.setCoordinateSize(coordinates)
detectionNetwork.setAnchors(anchors)
detectionNetwork.setAnchorMasks(anchorMasks)
detectionNetwork.setIouThreshold(iouThreshold)
detectionNetwork.setBlobPath(nnPath)
detectionNetwork.setNumInferenceThreads(2)
detectionNetwork.input.setBlocking(False)

# Linking
camRgb.preview.link(detectionNetwork.input)
detectionNetwork.passthrough.link(xoutRgb.input)
detectionNetwork.out.link(nnOut.input)
monoLeft.out.link(stereo.left)
monoRight.out.link(stereo.right)
stereo.depth.link(xoutDepth.input)
monoLeft.out.link(xoutLeft.input)
monoRight.out.link(xoutRight.input)
camRgb.video.link(xoutVideo.input)

# Define ROIs (fork 카메라 전용)
rois = {
    "TM": (615 + 25, 160 + 325, 25, 25),  # Top Middle
    "MR": (815 + 25, 360 + 25, 25, 25),  # Middle Right
    "MM": (615 + 25, 360 + 25, 25, 25),  # Middle Middle
    "ML": (415 + 25, 360 + 25, 25, 25),  # Middle Left
    "BR": (915 + 25, 560 + 25, 25, 25),  # Bottom Right
    "BM": (615 + 25, 560 + 25, 25, 25),  # Bottom Middle
    "BL": (315 + 25, 560 + 25, 25, 25)   # Bottom Left
}

# Function to calculate ROI depths (fork 카메라 전용)
def calculate_roi_depths(depthFrame, rois):
    roi_depths = {}
    for key, (x, y, w, h) in rois.items():
        roi = depthFrame[y:y+h, x:x+w]
        non_zero_values = roi[roi > 0]
        if len(non_zero_values) > 0:
            average_depth = non_zero_values.mean()
            if average_depth >= 7000:  # 7m 이상이면 -1로 설정
                average_depth = -1
        else:
            average_depth = 0  # 모든 값이 0인 경우 0으로 설정
        roi_depths[key] = int(average_depth)  # 정수형으로 변환
    return roi_depths

# nn data, being the bounding box locations, are in <0..1> range - they need to be normalized with frame width/height
def frameNorm(frame, bbox):
    normVals = np.full(len(bbox), frame.shape[0])
    normVals[::2] = frame.shape[1]
    return (np.clip(np.array(bbox), 0, 1) * normVals).astype(int)

def displayNNFrame(name, frame, detections, distances):
    for detection, distance in zip(detections, distances):
        if labels[detection.label] == "person":  # Only process 'person' detections
            bbox = frameNorm(frame, (detection.xmin, detection.ymin, detection.xmax, detection.ymax))
            x_center = int((bbox[0] + bbox[2]) / 2)
            y_center = int((bbox[1] + bbox[3]) / 2)
            distance_m = distance / 1000  # mm를 m로 변환
            
            if distance_m < 5 and distance_m > 1:  # 1 ~ 5m 이내는 위험, 1m 이하는 판단 불가
                color = (0, 0, 255)  # Red color for dangerous
                label_text = "person"
            else:
                color = (0, 255, 0)  # Green color for normal
                label_text = "person"
            cv2.putText(frame, f"{label_text} {int(detection.confidence * 100)}%", (bbox[0] + 10, bbox[1] + 20), cv2.FONT_HERSHEY_TRIPLEX, 0.5, color)
            cv2.putText(frame, f"dist: {distance_m:.2f} m", (bbox[0] + 10, bbox[1] + 40), cv2.FONT_HERSHEY_TRIPLEX, 0.5, color)
            cv2.rectangle(frame, (bbox[0], bbox[1]), (bbox[2], bbox[3]), color, 2)
    cv2.imshow(name, frame)

def displayRgbFrame(name, frame, depthFrame, roi_depths):
    # Normalize and colorize depth frame
    depthFrameColor = cv2.normalize(depthFrame, None, 0, 255, cv2.NORM_MINMAX)
    depthFrameColor = cv2.applyColorMap(depthFrameColor.astype(np.uint8), cv2.COLORMAP_JET)
    
    # Apply transparency to depth frame
    alpha = 0.6
    beta = 1 - alpha
    combinedFrame = cv2.addWeighted(frame, beta, depthFrameColor, alpha, 0)

    for key, (x, y, w, h) in rois.items():
        cv2.rectangle(combinedFrame, (x, y), (x + w, y + h), (0, 255, 0), 2)  # Green color for ROIs
        cv2.putText(combinedFrame, key, (x, y - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 1)
        if key in roi_depths:
            depth_mm = roi_depths[key]
            if depth_mm == -1:
                depth_text = "-1"
            else:
                depth_text = f"{depth_mm} mm"
            cv2.putText(combinedFrame, depth_text, (x, y + h + 20), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 1)
    cv2.imshow(name, combinedFrame)

def displayDepthFrame(name, depthFrame, detections):
    distances.clear()  # 이전 프레임의 distances 리스트를 초기화
    for detection in detections:
        if labels[detection.label] == "person":
            bbox = frameNorm(depthFrame, (detection.xmin, detection.ymin, detection.xmax, detection.ymax))
            x_center = int((bbox[0] + bbox[2]) / 2)
            y_center = int((bbox[1] + bbox[3]) / 2)
            # 중심점 근처의 작은 영역(예: 5x5 픽셀)의 깊이 값을 평균하여 distance 값을 계산
            region_size = 5
            x_start = max(0, x_center - region_size // 2)
            y_start = max(0, y_center - region_size // 2)
            x_end = min(depthFrame.shape[1], x_center + region_size // 2)
            y_end = min(depthFrame.shape[0], y_center + region_size // 2)
            distance = depthFrame[y_start:y_end, x_start:x_end].mean()
            distances.append(int(distance) if distance < 7000 else -1)  # 7m 이상이면 -1로 설정

# ZeroMQ context (나중에 device type에 따라 소켓 바인딩)
context = zmq.Context()

# Connect to device and start pipeline
with dai.Device(pipeline) as device:
    # device의 mxid를 읽어 카메라 타입(front, rear, fork) 결정
    device_mxid = device.getMxId()
    if device_mxid == front_mxid:
        camera_type = "front"
        base_port = 5570  # rgb:5570, left:5571, right:5572
    elif device_mxid == rear_mxid:
        camera_type = "rear"
        base_port = 5580  # rgb:5580, left:5581, right:5582
    else:
        camera_type = "fork"

    # 카메라 타입에 따라 ZeroMQ 소켓 바인딩
    if camera_type == "fork":
        socket_rgb = context.socket(zmq.PUB)
        socket_rgb.bind("tcp://*:5560")
        socket_left = context.socket(zmq.PUB)
        socket_left.bind("tcp://*:5561")
        socket_right = context.socket(zmq.PUB)
        socket_right.bind("tcp://*:5562")
        socket_depth = context.socket(zmq.PUB)
        socket_depth.bind("tcp://*:5563")
        socket_detection = context.socket(zmq.PUB)
        socket_detection.bind("tcp://*:5564")
        socket_video = context.socket(zmq.PUB)
        socket_video.bind("tcp://*:5565")
    else:
        socket_rgb = context.socket(zmq.PUB)
        socket_rgb.bind(f"tcp://*:{base_port}")
        socket_left = context.socket(zmq.PUB)
        socket_left.bind(f"tcp://*:{base_port+1}")
        socket_right = context.socket(zmq.PUB)
        socket_right.bind(f"tcp://*:{base_port+2}")

    # Output queues
    qRgb = device.getOutputQueue(name="rgb", maxSize=4, blocking=False)
    qLeft = device.getOutputQueue(name="left", maxSize=4, blocking=False)
    qRight = device.getOutputQueue(name="right", maxSize=4, blocking=False)
    if camera_type == "fork":
        qDet = device.getOutputQueue(name="nn", maxSize=4, blocking=False)
        qDepth = device.getOutputQueue(name="depth", maxSize=4, blocking=False)
        qVideo = device.getOutputQueue(name="video", maxSize=4, blocking=False)

    # fork 카메라의 경우: 기존 코드 그대로 실행
    if camera_type == "fork":
        frame = None
        detections = []
        distances = []  # 각 검출된 객체의 distance 값을 저장할 리스트
        startTime = time.monotonic()
        counter = 0
        color2 = (255, 255, 255)
        fps = 0
        prevTime = time.monotonic()

        while True:
            inRgb = qRgb.get()
            inDet = qDet.get()
            inDepth = qDepth.get()
            inLeft = qLeft.get()
            inRight = qRight.get()
            inVideo = qVideo.get()

            if inRgb is not None:
                frame = inRgb.getCvFrame()
                currentTime = time.monotonic()
                fps = 1 / (currentTime - prevTime)
                prevTime = currentTime
                cv2.putText(frame, f"RGB FPS: {fps:.2f}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1)
                cv2.putText(frame, "NN FPS: {:.2f}".format(counter / (time.monotonic() - startTime)),
                            (10, 50), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1)
                # Serialize and send RGB frame
                _, buffer = cv2.imencode('.jpg', frame)
                data_rgb = buffer.tobytes()
                # Send metadata as JSON
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_rgb = json.dumps({"timestamp": timestamp})
                socket_rgb.send_multipart([data_rgb, metadata_rgb.encode('utf-8')])

            if inDet is not None:
                detections = inDet.detections
                counter += 1
                # Serialize and send detection data as JSON
                detection_data = [{"label": labels[d.label], "confidence": d.confidence, "xmin": d.xmin, "ymin": d.ymin, "xmax": d.xmax, "ymax": d.ymax, "distance": dist} for d, dist in zip(detections, distances)]
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_detection = json.dumps({"timestamp": timestamp, "detections": detection_data})
                socket_detection.send_string(metadata_detection)

            if inDepth is not None:
                depthFrame = inDepth.getFrame()
                displayDepthFrame("depth", depthFrame, detections)
                
                # Calculate ROI depths
                roi_depths = calculate_roi_depths(depthFrame, rois)
                
                # Serialize and send depth frame
                _, buffer = cv2.imencode('.jpg', depthFrame)
                data_depth = buffer.tobytes()
                # Send metadata as JSON
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_depth = json.dumps({"timestamp": timestamp, "roi_depths": roi_depths})
                socket_depth.send_multipart([data_depth, metadata_depth.encode('utf-8')])

            if inLeft is not None:
                leftFrame = inLeft.getCvFrame()
                # Serialize and send left frame
                _, buffer = cv2.imencode('.jpg', leftFrame)
                data_left = buffer.tobytes()
                # Send metadata as JSON
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_left = json.dumps({"timestamp": timestamp})
                socket_left.send_multipart([data_left, metadata_left.encode('utf-8')])

            if inRight is not None:
                rightFrame = inRight.getCvFrame()
                # Serialize and send right frame
                _, buffer = cv2.imencode('.jpg', rightFrame)
                data_right = buffer.tobytes()
                # Send metadata as JSON
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_right = json.dumps({"timestamp": timestamp})
                socket_right.send_multipart([data_right, metadata_right.encode('utf-8')])

            if inVideo is not None:
                videoFrame = inVideo.getCvFrame()
                # Serialize and send RGB frame
                _, buffer = cv2.imencode('.jpg', videoFrame)
                data_video = buffer.tobytes()
                # Send metadata as JSON
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_video = json.dumps({"timestamp": timestamp})
                socket_video.send_multipart([data_video, metadata_video.encode('utf-8')])
                displayRgbFrame("RGB_Depth_Frame", videoFrame, depthFrame, roi_depths)

            if frame is not None:
                displayNNFrame("NN_Frame", frame, detections, distances)

            if cv2.waitKey(1) == ord('q'):
                break

    else:
        # front 또는 rear 카메라: rgb, left, right 만 처리 (포트: base, base+1, base+2)
        while True:
            inRgb = qRgb.get()
            inLeft = qLeft.get()
            inRight = qRight.get()

            if inRgb is not None:
                frame = inRgb.getCvFrame()
                _, buffer = cv2.imencode('.jpg', frame)
                data_rgb = buffer.tobytes()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_rgb = json.dumps({"timestamp": timestamp})
                socket_rgb.send_multipart([data_rgb, metadata_rgb.encode('utf-8')])
                cv2.imshow("RGB", frame)

            if inLeft is not None:
                leftFrame = inLeft.getCvFrame()
                _, buffer = cv2.imencode('.jpg', leftFrame)
                data_left = buffer.tobytes()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_left = json.dumps({"timestamp": timestamp})
                socket_left.send_multipart([data_left, metadata_left.encode('utf-8')])
                cv2.imshow("Left", leftFrame)

            if inRight is not None:
                rightFrame = inRight.getCvFrame()
                _, buffer = cv2.imencode('.jpg', rightFrame)
                data_right = buffer.tobytes()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_right = json.dumps({"timestamp": timestamp})
                socket_right.send_multipart([data_right, metadata_right.encode('utf-8')])
                cv2.imshow("Right", rightFrame)

            if cv2.waitKey(1) == ord('q'):
                break

    cv2.destroyAllWindows()
