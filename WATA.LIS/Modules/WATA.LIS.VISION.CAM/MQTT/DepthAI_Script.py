from pathlib import Path
import cv2
import depthai as dai
import numpy as np
import time
import argparse
import json
import blobconverter
import zmq
import os
import threading
from datetime import datetime

# 지정된 fork, front, rear IP 주소로 연결
fork_info =  dai.DeviceInfo("192.168.1.101")
front_info = dai.DeviceInfo("192.168.2.101")
rear_info  = dai.DeviceInfo("2.1.4")

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
    nnPath = str(blobconverter.from_zoo(args.model, shaves=4, zoo_type="depthai", use_cache=True))
# sync outputs
syncNN = True

# Create pipeline
fork_pipeline = dai.Pipeline()
front_pipeline = dai.Pipeline()
rear_pipeline = dai.Pipeline()

# Define fork sources and outputs
fork_camRgb = fork_pipeline.create(dai.node.ColorCamera)
fork_detectionNetwork = fork_pipeline.create(dai.node.YoloDetectionNetwork)
fork_xoutRgb = fork_pipeline.create(dai.node.XLinkOut)
fork_nnOut = fork_pipeline.create(dai.node.XLinkOut)
fork_monoLeft = fork_pipeline.create(dai.node.MonoCamera)
fork_monoRight = fork_pipeline.create(dai.node.MonoCamera)
fork_stereo = fork_pipeline.create(dai.node.StereoDepth)
fork_xoutDepth = fork_pipeline.create(dai.node.XLinkOut)
fork_xoutLeft = fork_pipeline.create(dai.node.XLinkOut)
fork_xoutRight = fork_pipeline.create(dai.node.XLinkOut)
fork_xoutVideo = fork_pipeline.create(dai.node.XLinkOut)

fork_xoutRgb.setStreamName("rgb")
fork_nnOut.setStreamName("nn")
fork_xoutDepth.setStreamName("depth")
fork_xoutLeft.setStreamName("left")
fork_xoutRight.setStreamName("right")
fork_xoutVideo.setStreamName("video")

# RGB Camera
fork_camRgb.setPreviewSize(W, H)
fork_camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_720_P)
fork_camRgb.setInterleaved(False)
fork_camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
fork_camRgb.setFps(10)

# Left and Right mono cameras
fork_monoLeft.setBoardSocket(dai.CameraBoardSocket.CAM_B)
fork_monoLeft.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
fork_monoLeft.setFps(10)
fork_monoRight.setBoardSocket(dai.CameraBoardSocket.CAM_C)
fork_monoRight.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
fork_monoRight.setFps(10)

# StereoDepth settings
fork_stereo.setDefaultProfilePreset(dai.node.StereoDepth.PresetMode.ROBOTICS)
fork_stereo.initialConfig.setConfidenceThreshold(200)
fork_stereo.setLeftRightCheck(True)  # Enable left-right check
fork_stereo.setExtendedDisparity(False)  # Disable extended disparity
fork_stereo.setSubpixel(False)  # Disable subpixel
fork_stereo.setDepthAlign(dai.CameraBoardSocket.CAM_A)  # Align depth map to the RGB camera
fork_stereo.setOutputSize(1280, 720)  # Output size for depth map

# Network specific settings
fork_detectionNetwork.setConfidenceThreshold(confidenceThreshold)
fork_detectionNetwork.setNumClasses(classes)
fork_detectionNetwork.setCoordinateSize(coordinates)
fork_detectionNetwork.setAnchors(anchors)
fork_detectionNetwork.setAnchorMasks(anchorMasks)
fork_detectionNetwork.setIouThreshold(iouThreshold)
fork_detectionNetwork.setBlobPath(nnPath)
fork_detectionNetwork.setNumInferenceThreads(2)
fork_detectionNetwork.input.setBlocking(False)

# Linking
fork_camRgb.preview.link(fork_detectionNetwork.input)
fork_detectionNetwork.passthrough.link(fork_xoutRgb.input)
fork_detectionNetwork.out.link(fork_nnOut.input)
fork_monoLeft.out.link(fork_stereo.left)
fork_monoRight.out.link(fork_stereo.right)
fork_stereo.depth.link(fork_xoutDepth.input)
fork_monoLeft.out.link(fork_xoutLeft.input)
fork_monoRight.out.link(fork_xoutRight.input)
fork_camRgb.video.link(fork_xoutVideo.input)




# Define front sources and outputs
front_camRgb = front_pipeline.create(dai.node.ColorCamera)
front_detectionNetwork = front_pipeline.create(dai.node.YoloDetectionNetwork)
front_xoutRgb = front_pipeline.create(dai.node.XLinkOut)
front_nnOut = front_pipeline.create(dai.node.XLinkOut)
front_monoLeft = front_pipeline.create(dai.node.MonoCamera)
front_monoRight = front_pipeline.create(dai.node.MonoCamera)
front_stereo = front_pipeline.create(dai.node.StereoDepth)
front_xoutDepth = front_pipeline.create(dai.node.XLinkOut)
front_xoutLeft = front_pipeline.create(dai.node.XLinkOut)
front_xoutRight = front_pipeline.create(dai.node.XLinkOut)
front_xoutVideo = front_pipeline.create(dai.node.XLinkOut)

front_xoutRgb.setStreamName("rgb")
front_nnOut.setStreamName("nn")
front_xoutDepth.setStreamName("depth")
front_xoutLeft.setStreamName("left")
front_xoutRight.setStreamName("right")
front_xoutVideo.setStreamName("video")

# RGB Camera
front_camRgb.setPreviewSize(W, H)
front_camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_1080_P)
front_camRgb.setInterleaved(False)
front_camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
front_camRgb.setFps(10)

# Left and Right mono cameras
front_monoLeft.setBoardSocket(dai.CameraBoardSocket.CAM_B)
front_monoLeft.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
front_monoLeft.setFps(10)
front_monoRight.setBoardSocket(dai.CameraBoardSocket.CAM_C)
front_monoRight.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
front_monoRight.setFps(10)

# StereoDepth settings
front_stereo.setDefaultProfilePreset(dai.node.StereoDepth.PresetMode.ROBOTICS)
front_stereo.initialConfig.setConfidenceThreshold(200)
front_stereo.setLeftRightCheck(True)  # Enable left-right check
front_stereo.setExtendedDisparity(False)  # Disable extended disparity
front_stereo.setSubpixel(False)  # Disable subpixel
front_stereo.setDepthAlign(dai.CameraBoardSocket.CAM_A)  # Align depth map to the RGB camera
front_stereo.setOutputSize(1280, 720)  # Output size for depth map

# Network specific settings
front_detectionNetwork.setConfidenceThreshold(confidenceThreshold)
front_detectionNetwork.setNumClasses(classes)
front_detectionNetwork.setCoordinateSize(coordinates)
front_detectionNetwork.setAnchors(anchors)
front_detectionNetwork.setAnchorMasks(anchorMasks)
front_detectionNetwork.setIouThreshold(iouThreshold)
front_detectionNetwork.setBlobPath(nnPath)
front_detectionNetwork.setNumInferenceThreads(2)
front_detectionNetwork.input.setBlocking(False)

# Linking
front_camRgb.preview.link(front_detectionNetwork.input)
front_detectionNetwork.passthrough.link(front_xoutRgb.input)
front_detectionNetwork.out.link(front_nnOut.input)
front_monoLeft.out.link(front_stereo.left)
front_monoRight.out.link(front_stereo.right)
front_stereo.depth.link(front_xoutDepth.input)
front_monoLeft.out.link(front_xoutLeft.input)
front_monoRight.out.link(front_xoutRight.input)
front_camRgb.video.link(front_xoutVideo.input)




# Define rear sources and outputs
rear_camRgb = rear_pipeline.create(dai.node.ColorCamera)
rear_detectionNetwork = rear_pipeline.create(dai.node.YoloDetectionNetwork)
rear_xoutRgb = rear_pipeline.create(dai.node.XLinkOut)
rear_nnOut = rear_pipeline.create(dai.node.XLinkOut)
rear_monoLeft = rear_pipeline.create(dai.node.MonoCamera)
rear_monoRight = rear_pipeline.create(dai.node.MonoCamera)
rear_stereo = rear_pipeline.create(dai.node.StereoDepth)
rear_xoutDepth = rear_pipeline.create(dai.node.XLinkOut)
rear_xoutLeft = rear_pipeline.create(dai.node.XLinkOut)
rear_xoutRight = rear_pipeline.create(dai.node.XLinkOut)
rear_xoutVideo = rear_pipeline.create(dai.node.XLinkOut)

rear_xoutRgb.setStreamName("rgb")
rear_nnOut.setStreamName("nn")
rear_xoutDepth.setStreamName("depth")
rear_xoutLeft.setStreamName("left")
rear_xoutRight.setStreamName("right")
rear_xoutVideo.setStreamName("video")

# RGB Camera
rear_camRgb.setPreviewSize(W, H)
rear_camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_720_P)
rear_camRgb.setInterleaved(False)
rear_camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
rear_camRgb.setFps(10)

# Left and Right mono cameras
rear_monoLeft.setBoardSocket(dai.CameraBoardSocket.CAM_B)
rear_monoLeft.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
rear_monoLeft.setFps(10)
rear_monoRight.setBoardSocket(dai.CameraBoardSocket.CAM_C)
rear_monoRight.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
rear_monoRight.setFps(10)

# StereoDepth settings
rear_stereo.setDefaultProfilePreset(dai.node.StereoDepth.PresetMode.ROBOTICS)
rear_stereo.initialConfig.setConfidenceThreshold(200)
rear_stereo.setLeftRightCheck(True)  # Enable left-right check
rear_stereo.setExtendedDisparity(False)  # Disable extended disparity
rear_stereo.setSubpixel(False)  # Disable subpixel
rear_stereo.setDepthAlign(dai.CameraBoardSocket.CAM_A)  # Align depth map to the RGB camera
rear_stereo.setOutputSize(1280, 720)  # Output size for depth map

# Network specific settings
rear_detectionNetwork.setConfidenceThreshold(confidenceThreshold)
rear_detectionNetwork.setNumClasses(classes)
rear_detectionNetwork.setCoordinateSize(coordinates)
rear_detectionNetwork.setAnchors(anchors)
rear_detectionNetwork.setAnchorMasks(anchorMasks)
rear_detectionNetwork.setIouThreshold(iouThreshold)
rear_detectionNetwork.setBlobPath(nnPath)
rear_detectionNetwork.setNumInferenceThreads(2)
rear_detectionNetwork.input.setBlocking(False)

# Linking
rear_camRgb.preview.link(rear_detectionNetwork.input)
rear_detectionNetwork.passthrough.link(rear_xoutRgb.input)
rear_detectionNetwork.out.link(rear_nnOut.input)
rear_monoLeft.out.link(rear_stereo.left)
rear_monoRight.out.link(rear_stereo.right)
rear_stereo.depth.link(rear_xoutDepth.input)
rear_monoLeft.out.link(rear_xoutLeft.input)
rear_monoRight.out.link(rear_xoutRight.input)
rear_camRgb.video.link(rear_xoutVideo.input)




# ZeroMQ context and sockets
fork_context = zmq.Context()
front_context = zmq.Context()
rear_context = zmq.Context()

# 전역 변수로 distance 선언
fork_distance = 0
front_distance = 0
rear_distance = 0

# Define ROIs
fork_rois = {
    "BR": (730, 705, 10, 10),  # Bottom Right
    "BL": (530, 705, 10, 10)   # Bottom Left
}
front_rois = {
    "BR": (1160 + 25, 360 + 25, 25, 25),  # Bottom Right
    "BL": (760 + 25, 360 + 25, 25, 25)   # Bottom Left
}
rear_rois = {
    "BR": (915 + 25, 560 + 25, 25, 25),  # Bottom Right
    "BL": (315 + 25, 560 + 25, 25, 25)   # Bottom Left
}

# 전역 함수 정의
def calculate_roi_depths(depthFrame, rois):
    roi_depths = {}
    for key, (x, y, w, h) in rois.items():
        roi = depthFrame[y:y+h, x:x+w]
        non_zero_values = roi[roi > 0]
        if len(non_zero_values) > 0:
            average_depth = non_zero_values.mean()
            if average_depth >= 7500:  # 7.5m 이상이면 -1로 설정
                average_depth = -1
        else:
            average_depth = 0  # 모든 값이 0인 경우 0으로 설정
        roi_depths[key] = int(average_depth)  # 정수형으로 변환
    return roi_depths

def frameNorm(frame, bbox):
    normVals = np.full(len(bbox), frame.shape[0])
    normVals[::2] = frame.shape[1]
    return (np.clip(np.array(bbox), 0, 1) * normVals).astype(int)

def displayNNFrame(name, frame, detections, distances):
    for detection, distance in zip(detections, distances):
        if labels[detection.label] == "person":  # Only process 'person' detections
            bbox = frameNorm(frame, (detection.xmin, detection.ymin, detection.xmax, detection.ymax))
            distance_m = distance / 1000  # mm를 m로 변환
            
            if 1< distance_m < 5 :  # 1 ~ 5m 이내는 위험, 1m 이하는 판단 불가
                color = (0, 0, 255)  # Red color for dangerous
                label = "person"
            else:
                color = (0, 255, 0)  # Green color for normal
                label = "person"
            cv2.putText(frame, f"{label} {int(detection.confidence * 100)}%", (bbox[0] + 10, bbox[1] + 20), cv2.FONT_HERSHEY_TRIPLEX, 0.5, color)
            cv2.putText(frame, f"dist: {distance_m:.2f} m", (bbox[0] + 10, bbox[1] + 40), cv2.FONT_HERSHEY_TRIPLEX, 0.5, color)
            cv2.rectangle(frame, (bbox[0], bbox[1]), (bbox[2], bbox[3]), color, 2)
    cv2.imshow(name, frame)

def displayRgbFrame(name, frame, depthFrame, roi_depths, rois):
    if depthFrame is None:
        print("Depth frame is None. Skipping frame.")
        return

    # Normalize and colorize depth frame
    depthFrameColor = cv2.normalize(depthFrame, None, 0, 255, cv2.NORM_MINMAX)
    depthFrameColor = cv2.applyColorMap(depthFrameColor.astype(np.uint8), cv2.COLORMAP_JET)

    # Depth 프레임 크기 조정
    if depthFrameColor.shape[:2] != frame.shape[:2]:
        depthFrameColor = cv2.resize(depthFrameColor, (frame.shape[1], frame.shape[0]))

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
            cv2.putText(combinedFrame, depth_text, (x + w + 10, y + h // 2 + 1), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 1)
    cv2.imshow(name, combinedFrame)

def displayDepthFrame(name, depthFrame, detections, distances):
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
            
            # 0이 아닌 값만 선택하여 평균 계산
            region = depthFrame[y_start:y_end, x_start:x_end]
            non_zero_values = region[region > 0]  # 0보다 큰 값만 선택
            if len(non_zero_values) > 0:
                distance = non_zero_values.mean()
            else:
                distance = 0  # 모든 값이 0인 경우 0으로 설정
            
            distances.append(int(distance) if distance < 9000 else -1)  # 9m 이상이면 -1로 설정

# 카메라 처리 함수
def process_camera(name, rois, pipeline, device_info, context):
    global fork_distance, front_distance, rear_distance  # 전역 변수 참조

    # ZeroMQ 소켓 설정
    sockets = {
        "rgb": context.socket(zmq.PUB),
        "left": context.socket(zmq.PUB),
        "right": context.socket(zmq.PUB),
        "depth": context.socket(zmq.PUB),
        "detection": context.socket(zmq.PUB),
        "video": context.socket(zmq.PUB),
    }

    # 소켓 포트 매핑
    port_base = {
        "Fork": 5560,
        "Front": 5570,
        "Rear": 5580,
    }
    base_port = port_base[name]

    # 각 소켓에 포트 바인딩
    sockets["rgb"].bind(f"tcp://*:{base_port}")
    sockets["left"].bind(f"tcp://*:{base_port + 1}")
    sockets["right"].bind(f"tcp://*:{base_port + 2}")
    sockets["depth"].bind(f"tcp://*:{base_port + 3}")
    sockets["detection"].bind(f"tcp://*:{base_port + 4}")
    sockets["video"].bind(f"tcp://*:{base_port + 5}")

    with dai.Device(pipeline, devInfo=device_info, usb2Mode=False) as device:
        qRgb = device.getOutputQueue(name="rgb", maxSize=4, blocking=False)
        qDet = device.getOutputQueue(name="nn", maxSize=4, blocking=False)
        qDepth = device.getOutputQueue(name="depth", maxSize=4, blocking=False)
        qLeft = device.getOutputQueue(name="left", maxSize=4, blocking=False)
        qRight = device.getOutputQueue(name="right", maxSize=4, blocking=False)
        qVideo = device.getOutputQueue(name="video", maxSize=4, blocking=False)

        frame = None
        detections = []
        distances = []
        startTime = time.monotonic()
        counter = 0
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
                _, buffer = cv2.imencode('.jpg', frame)
                data_rgb = buffer.tobytes()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_rgb = json.dumps({"timestamp": timestamp})
                sockets["rgb"].send_multipart([data_rgb, metadata_rgb.encode('utf-8')])

            if inDet is not None:
                detections = inDet.detections
                counter += 1
                detection_data = [{"label": labels[d.label], "confidence": d.confidence, "xmin": d.xmin, "ymin": d.ymin, "xmax": d.xmax, "ymax": d.ymax, "distance": dist} for d, dist in zip(detections, distances)]
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_detection = json.dumps({"timestamp": timestamp, "detections": detection_data})
                sockets["detection"].send_string(metadata_detection)

            if inDepth is not None:
                depthFrame = inDepth.getFrame()
                displayDepthFrame(f"{name}_depth", depthFrame, detections, distances)
                roi_depths = calculate_roi_depths(depthFrame, rois)
                _, buffer = cv2.imencode('.jpg', depthFrame)
                data_depth = buffer.tobytes()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_depth = json.dumps({"timestamp": timestamp, "roi_depths": roi_depths})
                sockets["depth"].send_multipart([data_depth, metadata_depth.encode('utf-8')])

                # 전역 distance 업데이트
                if name == "Fork":
                    fork_distance = distances
                elif name == "Front":
                    front_distance = distances
                elif name == "Rear":
                    rear_distance = distances

            if inVideo is not None:
                videoFrame = inVideo.getCvFrame()
                _, buffer = cv2.imencode('.jpg', videoFrame)
                data_video = buffer.tobytes()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_video = json.dumps({"timestamp": timestamp})
                sockets["video"].send_multipart([data_video, metadata_video.encode('utf-8')])
                displayRgbFrame(f"{name}_RGB_Depth_Frame", videoFrame, depthFrame, roi_depths, rois)

            if frame is not None:
                # 전역 distance를 사용하여 NNFrame 표시
                if name == "Fork":
                    displayNNFrame(f"{name}_NN_Frame", frame, detections, fork_distance)
                elif name == "Front":
                    displayNNFrame(f"{name}_NN_Frame", frame, detections, front_distance)
                elif name == "Rear":
                    displayNNFrame(f"{name}_NN_Frame", frame, detections, rear_distance)

            if cv2.waitKey(1) == ord('q'):
                break

# 쓰레드 생성
fork_thread = threading.Thread(target=process_camera, args=("Fork", fork_rois, fork_pipeline, fork_info, fork_context))
front_thread = threading.Thread(target=process_camera, args=("Front", front_rois, front_pipeline, front_info, front_context))
rear_thread = threading.Thread(target=process_camera, args=("Rear", rear_rois, rear_pipeline, rear_info, rear_context))

# 메인 쓰레드가 종료되지 않도록 대기
fork_thread.start()
front_thread.start()
rear_thread.start()

# 메인 쓰레드가 종료되지 않도록 대기
fork_thread.join()
front_thread.join()
rear_thread.join()

cv2.destroyAllWindows()