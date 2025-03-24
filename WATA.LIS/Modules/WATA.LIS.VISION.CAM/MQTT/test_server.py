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
from datetime import datetime

# 지정된 mxid 값 (실제 네트워크 상의 장치 식별값 대신 예제로 사용)
fork_mxid = "14442C10C101CED200"  # fork 카메라 (나머지)
front_mxid = "19443010C180962E00"  # front 카메라
rear_mxid  = "19443010C142962E00"  # rear 카메라

# (TCP 주소는 카메라 역할에 따라 고정됨)
# fork 카메라는 5560~5565, front 카메라는 5570~5572, rear 카메라는 5580~5582를 사용
# (아래 코드에서는 각 파이프라인별로 ZeroMQ 소켓을 별도 context로 생성합니다.)

# Change working directory to script's directory
script_dir = os.path.dirname(os.path.abspath(__file__))
os.chdir(script_dir)

# parse arguments (여기서는 모델 및 config 파일 경로를 받음)
parser = argparse.ArgumentParser()
parser.add_argument("-m", "--model", default='yolov4_tiny_coco_416x416', type=str)
parser.add_argument("-c", "--config", default='./json/yolov4-tiny.json', type=str)
args = parser.parse_args()

# parse config file
with open(args.config) as f:
    config = json.load(f)
nnConfig = config.get("nn_config", {})
if "input_size" in nnConfig:
    W, H = tuple(map(int, nnConfig.get("input_size").split('x')))

metadata = nnConfig.get("NN_specific_metadata", {})
classes = metadata.get("classes", {})
coordinates = metadata.get("coordinates", {})
anchors = metadata.get("anchors", {})
anchorMasks = metadata.get("anchor_masks", {})
iouThreshold = metadata.get("iou_threshold", {})
confidenceThreshold = metadata.get("confidence_threshold", {})
labels = config.get("mappings", {}).get("labels", {})

# get model path (blob)
nnPath = args.model
if not Path(nnPath).exists():
    print(f"No blob found at {nnPath}. Using DepthAI model zoo.")
    nnPath = str(blobconverter.from_zoo(args.model, shaves=6, zoo_type="depthai", use_cache=True))
syncNN = True

# Create three pipelines for fork, front, rear
fork_pipeline = dai.Pipeline()
front_pipeline = dai.Pipeline()
rear_pipeline = dai.Pipeline()

# --- PIPELINE 설정 (fork_pipeline) ---
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

fork_camRgb.setPreviewSize(W, H)
fork_camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_720_P)
fork_camRgb.setInterleaved(False)
fork_camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
fork_camRgb.setFps(10)

fork_monoLeft.setBoardSocket(dai.CameraBoardSocket.CAM_B)
fork_monoLeft.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
fork_monoLeft.setFps(10)
fork_monoRight.setBoardSocket(dai.CameraBoardSocket.CAM_C)
fork_monoRight.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
fork_monoRight.setFps(10)

fork_stereo.setDefaultProfilePreset(dai.node.StereoDepth.PresetMode.ROBOTICS)
fork_stereo.initialConfig.setConfidenceThreshold(200)
fork_stereo.setLeftRightCheck(True)
fork_stereo.setExtendedDisparity(False)
fork_stereo.setSubpixel(False)
fork_stereo.setDepthAlign(dai.CameraBoardSocket.CAM_A)
fork_stereo.setOutputSize(1280, 720)

fork_detectionNetwork.setConfidenceThreshold(confidenceThreshold)
fork_detectionNetwork.setNumClasses(classes)
fork_detectionNetwork.setCoordinateSize(coordinates)
fork_detectionNetwork.setAnchors(anchors)
fork_detectionNetwork.setAnchorMasks(anchorMasks)
fork_detectionNetwork.setIouThreshold(iouThreshold)
fork_detectionNetwork.setBlobPath(nnPath)
fork_detectionNetwork.setNumInferenceThreads(2)
fork_detectionNetwork.input.setBlocking(False)

fork_camRgb.preview.link(fork_detectionNetwork.input)
fork_detectionNetwork.passthrough.link(fork_xoutRgb.input)
fork_detectionNetwork.out.link(fork_nnOut.input)
fork_monoLeft.out.link(fork_stereo.left)
fork_monoRight.out.link(fork_stereo.right)
fork_stereo.depth.link(fork_xoutDepth.input)
fork_monoLeft.out.link(fork_xoutLeft.input)
fork_monoRight.out.link(fork_xoutRight.input)
fork_camRgb.video.link(fork_xoutVideo.input)

# --- PIPELINE 설정 (front_pipeline) ---
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

front_camRgb.setPreviewSize(W, H)
front_camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_720_P)
front_camRgb.setInterleaved(False)
front_camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
front_camRgb.setFps(10)

front_monoLeft.setBoardSocket(dai.CameraBoardSocket.CAM_B)
front_monoLeft.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
front_monoLeft.setFps(10)
front_monoRight.setBoardSocket(dai.CameraBoardSocket.CAM_C)
front_monoRight.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
front_monoRight.setFps(10)

front_stereo.setDefaultProfilePreset(dai.node.StereoDepth.PresetMode.ROBOTICS)
front_stereo.initialConfig.setConfidenceThreshold(200)
front_stereo.setLeftRightCheck(True)
front_stereo.setExtendedDisparity(False)
front_stereo.setSubpixel(False)
front_stereo.setDepthAlign(dai.CameraBoardSocket.CAM_A)
front_stereo.setOutputSize(1280, 720)

front_detectionNetwork.setConfidenceThreshold(confidenceThreshold)
front_detectionNetwork.setNumClasses(classes)
front_detectionNetwork.setCoordinateSize(coordinates)
front_detectionNetwork.setAnchors(anchors)
front_detectionNetwork.setAnchorMasks(anchorMasks)
front_detectionNetwork.setIouThreshold(iouThreshold)
front_detectionNetwork.setBlobPath(nnPath)
front_detectionNetwork.setNumInferenceThreads(2)
front_detectionNetwork.input.setBlocking(False)

front_camRgb.preview.link(front_detectionNetwork.input)
front_detectionNetwork.passthrough.link(front_xoutRgb.input)
front_detectionNetwork.out.link(front_nnOut.input)
front_monoLeft.out.link(front_stereo.left)
front_monoRight.out.link(front_stereo.right)
front_stereo.depth.link(front_xoutDepth.input)
front_monoLeft.out.link(front_xoutLeft.input)
front_monoRight.out.link(front_xoutRight.input)
front_camRgb.video.link(front_xoutVideo.input)

# --- PIPELINE 설정 (rear_pipeline) ---
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

rear_camRgb.setPreviewSize(W, H)
rear_camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_720_P)
rear_camRgb.setInterleaved(False)
rear_camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
rear_camRgb.setFps(10)

rear_monoLeft.setBoardSocket(dai.CameraBoardSocket.CAM_B)
rear_monoLeft.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
rear_monoLeft.setFps(10)
rear_monoRight.setBoardSocket(dai.CameraBoardSocket.CAM_C)
rear_monoRight.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
rear_monoRight.setFps(10)

rear_stereo.setDefaultProfilePreset(dai.node.StereoDepth.PresetMode.ROBOTICS)
rear_stereo.initialConfig.setConfidenceThreshold(200)
rear_stereo.setLeftRightCheck(True)
rear_stereo.setExtendedDisparity(False)
rear_stereo.setSubpixel(False)
rear_stereo.setDepthAlign(dai.CameraBoardSocket.CAM_A)
rear_stereo.setOutputSize(1280, 720)

rear_detectionNetwork.setConfidenceThreshold(confidenceThreshold)
rear_detectionNetwork.setNumClasses(classes)
rear_detectionNetwork.setCoordinateSize(coordinates)
rear_detectionNetwork.setAnchors(anchors)
rear_detectionNetwork.setAnchorMasks(anchorMasks)
rear_detectionNetwork.setIouThreshold(iouThreshold)
rear_detectionNetwork.setBlobPath(nnPath)
rear_detectionNetwork.setNumInferenceThreads(2)
rear_detectionNetwork.input.setBlocking(False)

rear_camRgb.preview.link(rear_detectionNetwork.input)
rear_detectionNetwork.passthrough.link(rear_xoutRgb.input)
rear_detectionNetwork.out.link(rear_nnOut.input)
rear_monoLeft.out.link(rear_stereo.left)
rear_monoRight.out.link(rear_stereo.right)
rear_stereo.depth.link(rear_xoutDepth.input)
rear_monoLeft.out.link(rear_xoutLeft.input)
rear_monoRight.out.link(rear_xoutRight.input)
rear_camRgb.video.link(rear_xoutVideo.input)

# --- ZeroMQ 소켓 설정 ---
fork_context = zmq.Context()
front_context = zmq.Context()
rear_context = zmq.Context()

# fork 카메라 소켓 (5560~5565)
socket_fork_rgb = fork_context.socket(zmq.PUB)
socket_fork_rgb.bind("tcp://*:5560")
socket_fork_left = fork_context.socket(zmq.PUB)
socket_fork_left.bind("tcp://*:5561")
socket_fork_right = fork_context.socket(zmq.PUB)
socket_fork_right.bind("tcp://*:5562")
socket_fork_depth = fork_context.socket(zmq.PUB)
socket_fork_depth.bind("tcp://*:5563")
socket_fork_detection = fork_context.socket(zmq.PUB)
socket_fork_detection.bind("tcp://*:5564")
socket_fork_video = fork_context.socket(zmq.PUB)
socket_fork_video.bind("tcp://*:5565")

# front 카메라 소켓 (5570~5572)
socket_front_rgb = front_context.socket(zmq.PUB)
socket_front_rgb.bind("tcp://*:5570")
socket_front_left = front_context.socket(zmq.PUB)
socket_front_left.bind("tcp://*:5571")
socket_front_right = front_context.socket(zmq.PUB)
socket_front_right.bind("tcp://*:5572")
# (필요시 front_depth, front_detection, front_video 소켓도 추가 가능)

# rear 카메라 소켓 (5580~5582)
socket_rear_rgb = rear_context.socket(zmq.PUB)
socket_rear_rgb.bind("tcp://*:5580")
socket_rear_left = rear_context.socket(zmq.PUB)
socket_rear_left.bind("tcp://*:5581")
socket_rear_right = rear_context.socket(zmq.PUB)
socket_rear_right.bind("tcp://*:5582")
# (필요시 rear_depth, rear_detection, rear_video 소켓도 추가 가능)

# 전역 변수: 거리 정보
fork_distance = 0
front_distance = 0
rear_distance = 0

# ROIs 설정 (각 카메라별로 동일하게 설정)
fork_rois = front_rois = rear_rois = {
    "TM": (640, 485, 25, 25),
    "MR": (840, 385, 25, 25),
    "MM": (640, 385, 25, 25),
    "ML": (440, 385, 25, 25),
    "BR": (1040, 285, 25, 25),
    "BM": (640, 285, 25, 25),
    "BL": (240, 285, 25, 25)
}

def calculate_roi_depths(depthFrame, rois):
    roi_depths = {}
    for key, (x, y, w, h) in rois.items():
        roi = depthFrame[y:y+h, x:x+w]
        non_zero = roi[roi > 0]
        if non_zero.size > 0:
            avg = non_zero.mean()
            if avg >= 7000:
                avg = -1
        else:
            avg = 0
        roi_depths[key] = int(avg)
    return roi_depths

# --- Fork 카메라 파이프라인 실행 (기존 코드 그대로) ---
with dai.Device(fork_pipeline) as device:
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

    def frameNorm(frame, bbox):
        normVals = np.full(len(bbox), frame.shape[0])
        normVals[::2] = frame.shape[1]
        return (np.clip(np.array(bbox), 0, 1) * normVals).astype(int)

    def displayNNFrame(name, frame, detections, distances):
        for detection, distance in zip(detections, distances):
            if labels[detection.label] == "person":
                bbox = frameNorm(frame, (detection.xmin, detection.ymin, detection.xmax, detection.ymax))
                cv2.rectangle(frame, (bbox[0], bbox[1]), (bbox[2], bbox[3]), (0,255,0), 2)
        cv2.imshow(name, frame)

    def displayRgbFrame(name, frame, depthFrame, roi_depths):
        depthColor = cv2.normalize(depthFrame, None, 0, 255, cv2.NORM_MINMAX)
        depthColor = cv2.applyColorMap(depthColor.astype(np.uint8), cv2.COLORMAP_JET)
        combined = cv2.addWeighted(frame, 0.4, depthColor, 0.6, 0)
        cv2.imshow(name, combined)

    def displayDepthFrame(name, depthFrame, detections):
        cv2.imshow(name, depthFrame)

    while True:
        inRgb = qRgb.get()
        inDet = qDet.get()
        inDepth = qDepth.get()
        inLeft = qLeft.get()
        inRight = qRight.get()
        inVideo = qVideo.get()

        if inRgb is not None:
            frame = inRgb.getCvFrame()
            _, buf = cv2.imencode('.jpg', frame)
            socket_fork_rgb.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
        if inDet is not None:
            detections = inDet.detections
            socket_fork_detection.send_string(json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f"), "detections": [d.__dict__ for d in detections]}))
        if inDepth is not None:
            depthFrame = inDepth.getFrame()
            roi_depths = calculate_roi_depths(depthFrame, fork_rois)
            _, buf = cv2.imencode('.jpg', depthFrame)
            socket_fork_depth.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f"), "roi_depths": roi_depths}).encode('utf-8')])
        if inLeft is not None:
            leftFrame = inLeft.getCvFrame()
            _, buf = cv2.imencode('.jpg', leftFrame)
            socket_fork_left.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
        if inRight is not None:
            rightFrame = inRight.getCvFrame()
            _, buf = cv2.imencode('.jpg', rightFrame)
            socket_fork_right.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
        if inVideo is not None:
            videoFrame = inVideo.getCvFrame()
            _, buf = cv2.imencode('.jpg', videoFrame)
            socket_fork_video.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            displayRgbFrame("Fork_RGB_Depth_Frame", videoFrame, depthFrame, fork_rois)
        if frame is not None:
            displayNNFrame("Fork_NN_Frame", frame, detections, distances)
        if cv2.waitKey(1) == ord('q'):
            break
    cv2.destroyAllWindows()

# --- Front 카메라 파이프라인 실행 (front_mxid) ---
with dai.Device(front_pipeline) as device:
    qRgb = device.getOutputQueue(name="rgb", maxSize=4, blocking=False)
    qLeft = device.getOutputQueue(name="left", maxSize=4, blocking=False)
    qRight = device.getOutputQueue(name="right", maxSize=4, blocking=False)
    qDet = device.getOutputQueue(name="nn", maxSize=4, blocking=False)
    qDepth = device.getOutputQueue(name="depth", maxSize=4, blocking=False)
    qVideo = device.getOutputQueue(name="video", maxSize=4, blocking=False)

    while True:
        inRgb = qRgb.get()
        inLeft = qLeft.get()
        inRight = qRight.get()
        inVideo = qVideo.get()
        if inRgb is not None:
            frame = inRgb.getCvFrame()
            _, buf = cv2.imencode('.jpg', frame)
            socket_front_rgb.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            cv2.imshow("Front_RGB", frame)
        if inLeft is not None:
            leftFrame = inLeft.getCvFrame()
            _, buf = cv2.imencode('.jpg', leftFrame)
            socket_front_left.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            cv2.imshow("Front_Left", leftFrame)
        if inRight is not None:
            rightFrame = inRight.getCvFrame()
            _, buf = cv2.imencode('.jpg', rightFrame)
            socket_front_right.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            cv2.imshow("Front_Right", rightFrame)
        if inVideo is not None:
            videoFrame = inVideo.getCvFrame()
            _, buf = cv2.imencode('.jpg', videoFrame)
            socket_front_video.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            displayRgbFrame("Front_RGB_Depth_Frame", videoFrame, qDepth.get().getFrame(), front_rois)
        if cv2.waitKey(1) == ord('q'):
            break
    cv2.destroyAllWindows()

# --- Rear 카메라 파이프라인 실행 (rear_mxid) ---
with dai.Device(rear_pipeline) as device:
    qRgb = device.getOutputQueue(name="rgb", maxSize=4, blocking=False)
    qLeft = device.getOutputQueue(name="left", maxSize=4, blocking=False)
    qRight = device.getOutputQueue(name="right", maxSize=4, blocking=False)
    qDet = device.getOutputQueue(name="nn", maxSize=4, blocking=False)
    qDepth = device.getOutputQueue(name="depth", maxSize=4, blocking=False)
    qVideo = device.getOutputQueue(name="video", maxSize=4, blocking=False)

    while True:
        inRgb = qRgb.get()
        inLeft = qLeft.get()
        inRight = qRight.get()
        inVideo = qVideo.get()
        if inRgb is not None:
            frame = inRgb.getCvFrame()
            _, buf = cv2.imencode('.jpg', frame)
            socket_rear_rgb.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            cv2.imshow("Rear_RGB", frame)
        if inLeft is not None:
            leftFrame = inLeft.getCvFrame()
            _, buf = cv2.imencode('.jpg', leftFrame)
            socket_rear_left.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            cv2.imshow("Rear_Left", leftFrame)
        if inRight is not None:
            rightFrame = inRight.getCvFrame()
            _, buf = cv2.imencode('.jpg', rightFrame)
            socket_rear_right.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            cv2.imshow("Rear_Right", rightFrame)
        if inVideo is not None:
            videoFrame = inVideo.getCvFrame()
            _, buf = cv2.imencode('.jpg', videoFrame)
            socket_rear_video.send_multipart([buf.tobytes(), json.dumps({"timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")}).encode('utf-8')])
            displayRgbFrame("Rear_RGB_Depth_Frame", videoFrame, qDepth.get().getFrame(), rear_rois)
        if cv2.waitKey(1) == ord('q'):
            break
    cv2.destroyAllWindows()
