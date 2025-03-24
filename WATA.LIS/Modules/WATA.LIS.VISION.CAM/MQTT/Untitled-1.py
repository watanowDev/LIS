#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
통합 스크립트:
  - fork, front, rear 3개 카메라에서 DepthAI 스트림을 읽어 각 소켓별로 PUB 서버를 엽니다.
  - 동시에 ZeroMQ SUB 소켓을 이용한 모니터링 대시보드를 Tkinter GUI로 실행하여,
    각 소켓의 메시지 건수, 마지막 수신 시각, 미리보기, 구독자 수(알 수 없음)와 함께
    최근 2초 내에 메시지 수신 여부에 따라 “발행 상태”(발행중/정지)를 표시합니다.
  - 여러 물리적 디바이스가 연결된 경우, 사용자로 제공한 식별자(fork_ip, front_mxid, rear_mxid)를 기준으로
    각 파이프라인에 올바른 디바이스를 할당합니다.
  - 디바이스가 연결되지 않으면 “디바이스 연결중…” 로딩창을 표시합니다.
"""

import os, sys, time, json, threading, argparse, pickle
from pathlib import Path
from datetime import datetime
from concurrent.futures import ThreadPoolExecutor

import cv2
import numpy as np
import depthai as dai
import blobconverter
import zmq
import tkinter as tk
from tkinter import ttk

# --- 전역 스레드 풀 생성 (이미지 인코딩 최적화를 위함) ---
executor = ThreadPoolExecutor(max_workers=8)

# --- 이미지 인코딩 함수 (비동기 오프로드) ---
def encode_image(frame, quality=50):
    encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), quality]
    ret, buffer = cv2.imencode('.jpg', frame, encode_param)
    if ret:
        return buffer.tobytes()
    else:
        return None

# === 사용자 식별자 (카메라별 IP 또는 MXID) ===
fork_ip =  "192.168.1.101"
front_mxid = "192.168.2.101"
rear_mxid  = "2.1.4"

# 전역 변수 (디바이스 선택 후 할당)
available_devices = []
fork_device = None
front_device = None
rear_device = None

def get_device_by_identifier(identifier, used_devices=[]):
    # identifier가 dev.name 또는 dev.getMxId()와 일치하면 반환
    for dev in available_devices:
        if dev.name == identifier or dev.getMxId() == identifier:
            if dev not in used_devices:
                return dev
    return None

# === 디바이스 연결 체크 및 초기화 함수 ===
def initialize_devices():
    global available_devices, fork_device, front_device, rear_device
    available_devices = dai.Device.getAllAvailableDevices()
    if len(available_devices) == 0:
        return False

    used_devices = []
    fork_device = get_device_by_identifier(fork_ip)
    if fork_device is None:
        raise RuntimeError(f"Fork device with identifier {fork_ip} not found.")
    used_devices.append(fork_device)

    rear_device = get_device_by_identifier(rear_mxid, used_devices)
    if rear_device is None:
        raise RuntimeError(f"Rear device with identifier {rear_mxid} not found.")
    used_devices.append(rear_device)

    front_device = get_device_by_identifier(front_mxid, used_devices)
    if front_device is None:
        for dev in available_devices:
            if dev not in used_devices:
                front_device = dev
                break
    if front_device is None:
        raise RuntimeError("No available device found for front pipeline.")
    used_devices.append(front_device)

    print("선택된 디바이스 정보:")
    print("Fork device:", fork_device)
    print("Rear device:", rear_device)
    print("Front device:", front_device)
    return True

# === 2. 파이프라인 및 모델/설정 로드 ===
script_dir = os.path.dirname(os.path.abspath(__file__))
os.chdir(script_dir)

parser = argparse.ArgumentParser()
parser.add_argument("-m", "--model", help="Provide model name or model path for inference",
                    default='yolov4_tiny_coco_416x416', type=str)
parser.add_argument("-c", "--config", help="Provide config path for inference",
                    default='./json/yolov4-tiny.json', type=str)
args = parser.parse_args()

configPath = Path(args.config)
if not configPath.exists():
    raise ValueError("Path {} does not exist!".format(configPath))
with configPath.open() as f:
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

print("NN metadata:", metadata)

nnMappings = config.get("mappings", {})
labels = nnMappings.get("labels", {})

nnPath = args.model
if not Path(nnPath).exists():
    print("No blob found at {}. Looking into DepthAI model zoo.".format(nnPath))
    nnPath = str(blobconverter.from_zoo(args.model, shaves=6, zoo_type="depthai", use_cache=True))
syncNN = True

# 파이프라인 생성 (각각 fork, front, rear)
fork_pipeline = dai.Pipeline()
front_pipeline = dai.Pipeline()
rear_pipeline = dai.Pipeline()

# --- fork_pipeline 설정 ---
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

# --- front_pipeline 설정 (fork와 유사하게 구성) ---
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

# --- rear_pipeline 설정 (fork와 유사하게 구성) ---
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

# ROI 영역 (예시: fork, front, rear 동일)
fork_rois = {
    "TM": (640, 485, 25, 25),
    "MR": (840, 385, 25, 25),
    "MM": (640, 385, 25, 25),
    "ML": (440, 385, 25, 25),
    "BR": (940, 610, 25, 25),
    "BM": (640, 610, 25, 25),
    "BL": (340, 610, 25, 25)
}
front_rois = fork_rois.copy()
rear_rois = fork_rois.copy()

# === 3. ZeroMQ PUB 소켓 설정 ===
# fork 그룹 (5560~5565)
fork_context = zmq.Context()
socket_fork_rgb = fork_context.socket(zmq.PUB); socket_fork_rgb.bind("tcp://*:5560")
socket_fork_left = fork_context.socket(zmq.PUB); socket_fork_left.bind("tcp://*:5561")
socket_fork_right = fork_context.socket(zmq.PUB); socket_fork_right.bind("tcp://*:5562")
socket_fork_depth = fork_context.socket(zmq.PUB); socket_fork_depth.bind("tcp://*:5563")
socket_fork_detection = fork_context.socket(zmq.PUB); socket_fork_detection.bind("tcp://*:5564")
socket_fork_video = fork_context.socket(zmq.PUB); socket_fork_video.bind("tcp://*:5565")

# front 그룹 (5570~5575)
front_context = zmq.Context()
socket_front_rgb = front_context.socket(zmq.PUB); socket_front_rgb.bind("tcp://*:5570")
socket_front_left = front_context.socket(zmq.PUB); socket_front_left.bind("tcp://*:5571")
socket_front_right = front_context.socket(zmq.PUB); socket_front_right.bind("tcp://*:5572")
socket_front_depth = front_context.socket(zmq.PUB); socket_front_depth.bind("tcp://*:5573")
socket_front_detection = front_context.socket(zmq.PUB); socket_front_detection.bind("tcp://*:5574")
socket_front_video = front_context.socket(zmq.PUB); socket_front_video.bind("tcp://*:5575")

# rear 그룹 (5580~5585)
rear_context = zmq.Context()
socket_rear_rgb = rear_context.socket(zmq.PUB); socket_rear_rgb.bind("tcp://*:5580")
socket_rear_left = rear_context.socket(zmq.PUB); socket_rear_left.bind("tcp://*:5581")
socket_rear_right = rear_context.socket(zmq.PUB); socket_rear_right.bind("tcp://*:5582")
socket_rear_depth = rear_context.socket(zmq.PUB); socket_rear_depth.bind("tcp://*:5583")
socket_rear_detection = rear_context.socket(zmq.PUB); socket_rear_detection.bind("tcp://*:5584")
socket_rear_video = rear_context.socket(zmq.PUB); socket_rear_video.bind("tcp://*:5585")

# --- 소켓 상세 정보 설정 ---
endpoints = {
    "fork_rgb":       "tcp://localhost:5560",
    "fork_left":      "tcp://localhost:5561",
    "fork_right":     "tcp://localhost:5562",
    "fork_depth":     "tcp://localhost:5563",
    "fork_detection": "tcp://localhost:5564",
    "fork_video":     "tcp://localhost:5565",
    "front_rgb":      "tcp://localhost:5570",
    "front_left":     "tcp://localhost:5571",
    "front_right":    "tcp://localhost:5572",
    "front_depth":    "tcp://localhost:5573",
    "front_detection":"tcp://localhost:5574",
    "front_video":    "tcp://localhost:5575",
    "rear_rgb":       "tcp://localhost:5580",
    "rear_left":      "tcp://localhost:5581",
    "rear_right":     "tcp://localhost:5582",
    "rear_depth":     "tcp://localhost:5583",
    "rear_detection": "tcp://localhost:5584",
    "rear_video":     "tcp://localhost:5585",
}
socket_info = {}
for key, addr in endpoints.items():
    info = {}
    info["TCP Address"] = addr
    if "rgb" in key:
        info["Stream Type"] = "RGB"
        info["Resolution"] = f"{W}x{H}"
    elif "left" in key:
        info["Stream Type"] = "Stereo Left"
        info["Resolution"] = "1280x720"
    elif "right" in key:
        info["Stream Type"] = "Stereo Right"
        info["Resolution"] = "1280x720"
    elif "depth" in key:
        info["Stream Type"] = "Depth"
        info["Resolution"] = "1280x720"
    elif "detection" in key:
        info["Stream Type"] = "Detection"
        info["Resolution"] = "N/A"
    elif "video" in key:
        info["Stream Type"] = "Video"
        info["Resolution"] = f"{W}x{H}"
    else:
        info["Stream Type"] = "Unknown"
        info["Resolution"] = "N/A"
    socket_info[key] = info

# === 4. 공통 함수: ROI 깊이 계산 ===
def calculate_roi_depths(depthFrame, rois):
    roi_depths = {}
    for key, (x, y, w, h) in rois.items():
        roi = depthFrame[y:y+h, x:x+w]
        non_zero = roi[roi > 0]
        if len(non_zero) > 0:
            avg = non_zero.mean()
            if avg >= 7000:
                avg = -1
        else:
            avg = 0
        roi_depths[key] = int(avg)
    return roi_depths

# === 5. 각 카메라 파이프라인 실행 함수 (멀티스레딩으로 이미지 인코딩 최적화) ===
def run_fork_pipeline():
    with dai.Device(fork_pipeline, deviceInfo=fork_device) as device:
        qRgb = device.getOutputQueue(name="rgb", maxSize=4, blocking=False)
        qDet = device.getOutputQueue(name="nn", maxSize=4, blocking=False)
        qDepth = device.getOutputQueue(name="depth", maxSize=4, blocking=False)
        qLeft = device.getOutputQueue(name="left", maxSize=4, blocking=False)
        qRight = device.getOutputQueue(name="right", maxSize=4, blocking=False)
        qVideo = device.getOutputQueue(name="video", maxSize=4, blocking=False)
        
        frame = None
        detections = []
        distances = []
        prevTime = time.monotonic()
        roi_depths = {}

        def frameNorm(frame, bbox):
            normVals = np.full(len(bbox), frame.shape[0])
            normVals[::2] = frame.shape[1]
            return (np.clip(np.array(bbox), 0, 1) * normVals).astype(int)
        
        def displayNNFrame(name, frame, detections, distances):
            for detection, dist in zip(detections, distances):
                if labels[detection.label] == "person":
                    bbox = frameNorm(frame, (detection.xmin, detection.ymin, detection.xmax, detection.ymax))
                    distance_m = dist / 1000
                    color = (0, 0, 255) if (1 < distance_m < 5) else (0, 255, 0)
                    cv2.putText(frame, f"person {int(detection.confidence*100)}%", (bbox[0]+10, bbox[1]+20),
                                cv2.FONT_HERSHEY_TRIPLEX, 0.5, color)
                    cv2.putText(frame, f"dist: {distance_m:.2f} m", (bbox[0]+10, bbox[1]+40),
                                cv2.FONT_HERSHEY_TRIPLEX, 0.5, color)
                    cv2.rectangle(frame, (bbox[0], bbox[1]), (bbox[2], bbox[3]), color, 2)
            cv2.imshow(name, frame)
        
        def displayRgbFrame(name, frame, depthFrame, roi_depths):
            depthColor = cv2.normalize(depthFrame, None, 0, 255, cv2.NORM_MINMAX)
            depthColor = cv2.applyColorMap(depthColor.astype(np.uint8), cv2.COLORMAP_JET)
            combined = cv2.addWeighted(frame, 0.4, depthColor, 0.6, 0)
            for key, (x, y, w, h) in fork_rois.items():
                cv2.rectangle(combined, (x, y), (x+w, y+h), (0, 255, 0), 2)
                cv2.putText(combined, key, (x, y-10), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0,255,0), 1)
                if key in roi_depths:
                    d_text = "-1" if roi_depths[key] == -1 else f"{roi_depths[key]} mm"
                    cv2.putText(combined, d_text, (x, y+h+20), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0,255,0), 1)
            cv2.imshow(name, combined)
        
        def displayDepthFrame(name, depthFrame, detections):
            distances.clear()
            for detection in detections:
                if labels[detection.label] == "person":
                    bbox = frameNorm(depthFrame, (detection.xmin, detection.ymin, detection.xmax, detection.ymax))
                    x_center = (bbox[0]+bbox[2])//2
                    y_center = (bbox[1]+bbox[3])//2
                    region = depthFrame[max(0, y_center-2):min(depthFrame.shape[0], y_center+2),
                                        max(0, x_center-2):min(depthFrame.shape[1], x_center+2)]
                    d_val = region.mean()
                    distances.append(int(d_val) if d_val < 7000 else -1)
        
        while True:
            inRgb = qRgb.get()
            inDet = qDet.get()
            inDepth = qDepth.get()
            inLeft = qLeft.get()
            inRight = qRight.get()
            inVideo = qVideo.get()

            if inRgb is not None:
                frame = inRgb.getCvFrame()
                fps = 1 / (time.monotonic() - prevTime)
                prevTime = time.monotonic()
                cv2.putText(frame, f"RGB FPS: {fps:.2f}", (10,30),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255,255,255), 1)
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_rgb = json.dumps({"timestamp": timestamp})
                # 이미지 인코딩을 스레드 풀에 오프로드
                future_rgb = executor.submit(encode_image, frame, 5)
                rgb_bytes = future_rgb.result()
                socket_fork_rgb.send_multipart([rgb_bytes, metadata_rgb.encode('utf-8')])
            
            if inDet is not None:
                detections = inDet.detections
                # 인코딩은 하지 않고 메타데이터만 전송
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                detection_data = [{"label": labels[d.label], "confidence": d.confidence,
                                   "xmin": d.xmin, "ymin": d.ymin, "xmax": d.xmax, "ymax": d.ymax,
                                   "distance": dist} for d, dist in zip(detections, distances)]
                metadata_det = json.dumps({"timestamp": timestamp, "detections": detection_data})
                socket_fork_detection.send_string(metadata_det)
            
            if inDepth is not None:
                depthFrame = inDepth.getFrame()
                displayDepthFrame("Fork_depth", depthFrame, detections)
                roi_depths = calculate_roi_depths(depthFrame, fork_rois)
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_depth = json.dumps({"timestamp": timestamp, "roi_depths": roi_depths})
                future_depth = executor.submit(encode_image, depthFrame)
                depth_bytes = future_depth.result()
                socket_fork_depth.send_multipart([depth_bytes, metadata_depth.encode('utf-8')])
            
            if inLeft is not None:
                leftFrame = inLeft.getCvFrame()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_left = json.dumps({"timestamp": timestamp})
                future_left = executor.submit(encode_image, leftFrame)
                left_bytes = future_left.result()
                socket_fork_left.send_multipart([left_bytes, metadata_left.encode('utf-8')])
            
            if inRight is not None:
                rightFrame = inRight.getCvFrame()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_right = json.dumps({"timestamp": timestamp})
                future_right = executor.submit(encode_image, rightFrame)
                right_bytes = future_right.result()
                socket_fork_right.send_multipart([right_bytes, metadata_right.encode('utf-8')])
            
            if inVideo is not None:
                videoFrame = inVideo.getCvFrame()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata_video = json.dumps({"timestamp": timestamp})
                future_video = executor.submit(encode_image, videoFrame)
                video_bytes = future_video.result()
                socket_fork_video.send_multipart([video_bytes, metadata_video.encode('utf-8')])
                displayRgbFrame("Fork_RGB_Depth", videoFrame, depthFrame, roi_depths)
            
            if frame is not None:
                displayNNFrame("Fork_NN", frame, detections, distances)
            
            if cv2.waitKey(1) == ord('q'):
                break
        cv2.destroyAllWindows()

def run_front_pipeline():
    with dai.Device(front_pipeline, deviceInfo=front_device) as device:
        qRgb = device.getOutputQueue(name="rgb", maxSize=4, blocking=False)
        qDet = device.getOutputQueue(name="nn", maxSize=4, blocking=False)
        qDepth = device.getOutputQueue(name="depth", maxSize=4, blocking=False)
        qLeft = device.getOutputQueue(name="left", maxSize=4, blocking=False)
        qRight = device.getOutputQueue(name="right", maxSize=4, blocking=False)
        qVideo = device.getOutputQueue(name="video", maxSize=4, blocking=False)
        
        frame = None; detections = []; distances = []
        prevTime = time.monotonic()
        roi_depths = {}
        def frameNorm(frame, bbox):
            normVals = np.full(len(bbox), frame.shape[0])
            normVals[::2] = frame.shape[1]
            return (np.clip(np.array(bbox), 0, 1) * normVals).astype(int)
        while True:
            inRgb = qRgb.get()
            inDet = qDet.get()
            inDepth = qDepth.get()
            inLeft = qLeft.get()
            inRight = qRight.get()
            inVideo = qVideo.get()
            if inRgb is not None:
                frame = inRgb.getCvFrame()
                fps = 1 / (time.monotonic() - prevTime)
                prevTime = time.monotonic()
                cv2.putText(frame, f"RGB FPS: {fps:.2f}", (10,30),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255,255,255), 1)
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp})
                future_rgb = executor.submit(encode_image, frame)
                rgb_bytes = future_rgb.result()
                socket_front_rgb.send_multipart([rgb_bytes, metadata.encode('utf-8')])
            if inDet is not None:
                detections = inDet.detections
                detection_data = [{"label": labels[d.label], "confidence": d.confidence,
                                   "xmin": d.xmin, "ymin": d.ymin, "xmax": d.xmax, "ymax": d.ymax,
                                   "distance": dist} for d, dist in zip(detections, distances)]
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp, "detections": detection_data})
                socket_front_detection.send_string(metadata)
            if inDepth is not None:
                depthFrame = inDepth.getFrame()
                roi_depths = calculate_roi_depths(depthFrame, front_rois)
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp, "roi_depths": roi_depths})
                future_depth = executor.submit(encode_image, depthFrame)
                depth_bytes = future_depth.result()
                socket_front_depth.send_multipart([depth_bytes, metadata.encode('utf-8')])
            if inLeft is not None:
                leftFrame = inLeft.getCvFrame()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp})
                future_left = executor.submit(encode_image, leftFrame)
                left_bytes = future_left.result()
                socket_front_left.send_multipart([left_bytes, metadata.encode('utf-8')])
            if inRight is not None:
                rightFrame = inRight.getCvFrame()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp})
                future_right = executor.submit(encode_image, rightFrame)
                right_bytes = future_right.result()
                socket_front_right.send_multipart([right_bytes, metadata.encode('utf-8')])
            if inVideo is not None:
                videoFrame = inVideo.getCvFrame()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp})
                future_video = executor.submit(encode_image, videoFrame)
                video_bytes = future_video.result()
                socket_front_video.send_multipart([video_bytes, metadata.encode('utf-8')])
            if cv2.waitKey(1) == ord('q'):
                break
        cv2.destroyAllWindows()

def run_rear_pipeline():
    with dai.Device(rear_pipeline, deviceInfo=rear_device) as device:
        qRgb = device.getOutputQueue(name="rgb", maxSize=4, blocking=False)
        qDet = device.getOutputQueue(name="nn", maxSize=4, blocking=False)
        qDepth = device.getOutputQueue(name="depth", maxSize=4, blocking=False)
        qLeft = device.getOutputQueue(name="left", maxSize=4, blocking=False)
        qRight = device.getOutputQueue(name="right", maxSize=4, blocking=False)
        qVideo = device.getOutputQueue(name="video", maxSize=4, blocking=False)
        
        frame = None; detections = []; distances = []
        prevTime = time.monotonic()
        roi_depths = {}
        def frameNorm(frame, bbox):
            normVals = np.full(len(bbox), frame.shape[0])
            normVals[::2] = frame.shape[1]
            return (np.clip(np.array(bbox), 0, 1) * normVals).astype(int)
        while True:
            inRgb = qRgb.get()
            inDet = qDet.get()
            inDepth = qDepth.get()
            inLeft = qLeft.get()
            inRight = qRight.get()
            inVideo = qVideo.get()
            if inRgb is not None:
                frame = inRgb.getCvFrame()
                frame = cv2.rotate(frame, cv2.ROTATE_180)  # 180도 회전 적용
                fps = 1 / (time.monotonic() - prevTime)
                prevTime = time.monotonic()
                cv2.putText(frame, f"RGB FPS: {fps:.2f}", (10,30),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255,255,255), 1)
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp})
                future_rgb = executor.submit(encode_image, frame)
                rgb_bytes = future_rgb.result()
                socket_rear_rgb.send_multipart([rgb_bytes, metadata.encode('utf-8')])
            if inDet is not None:
                detections = inDet.detections
                detection_data = [{"label": labels[d.label], "confidence": d.confidence,
                                   "xmin": d.xmin, "ymin": d.ymin, "xmax": d.xmax, "ymax": d.ymax,
                                   "distance": dist} for d, dist in zip(detections, distances)]
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp, "detections": detection_data})
                socket_rear_detection.send_string(metadata)
            if inDepth is not None:
                depthFrame = inDepth.getFrame()
                roi_depths = calculate_roi_depths(depthFrame, rear_rois)
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp, "roi_depths": roi_depths})
                future_depth = executor.submit(encode_image, depthFrame)
                depth_bytes = future_depth.result()
                socket_rear_depth.send_multipart([depth_bytes, metadata.encode('utf-8')])
            if inLeft is not None:
                leftFrame = inLeft.getCvFrame()
                future_left = executor.submit(encode_image, leftFrame)
                left_bytes = future_left.result()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp})
                socket_rear_left.send_multipart([left_bytes, metadata.encode('utf-8')])
            if inRight is not None:
                rightFrame = inRight.getCvFrame()
                future_right = executor.submit(encode_image, rightFrame)
                right_bytes = future_right.result()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp})
                socket_rear_right.send_multipart([right_bytes, metadata.encode('utf-8')])
            if inVideo is not None:
                videoFrame = inVideo.getCvFrame()
                videoFrame = cv2.rotate(videoFrame, cv2.ROTATE_180)  # 180도 회전 적용
                future_video = executor.submit(encode_image, videoFrame)
                video_bytes = future_video.result()
                timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")
                metadata = json.dumps({"timestamp": timestamp})
                socket_rear_video.send_multipart([video_bytes, metadata.encode('utf-8')])
            if cv2.waitKey(1) == ord('q'):
                break
        cv2.destroyAllWindows()

# === 6. 모니터링 대시보드 (ZeroMQ SUB + Tkinter GUI) ===

# PUB 소켓 엔드포인트 정보는 endpoints 딕셔너리 사용
monitor_data = {}
sockets_sub = {}
context_sub = zmq.Context()
for name, endpoint in endpoints.items():
    sock = context_sub.socket(zmq.SUB)
    sock.connect(endpoint)
    sock.setsockopt_string(zmq.SUBSCRIBE, "")
    sockets_sub[name] = sock
    monitor_data[name] = {
        "msg_count": 0,
        "last_time": "",
        "preview": "",
        "sub_count": "알 수 없음",
        "last_timestamp": 0.0
    }

poller = zmq.Poller()
for sock in sockets_sub.values():
    poller.register(sock, zmq.POLLIN)

def zmq_listener():
    global monitor_data
    while True:
        events = dict(poller.poll(100))
        for name, sock in sockets_sub.items():
            if sock in events and events[sock] == zmq.POLLIN:
                try:
                    msg = sock.recv_multipart(flags=zmq.NOBLOCK)
                    monitor_data[name]["msg_count"] += 1
                    now = time.time()
                    monitor_data[name]["last_timestamp"] = now
                    monitor_data[name]["last_time"] = datetime.fromtimestamp(now).strftime("%H:%M:%S")
                    if msg and len(msg) > 0:
                        monitor_data[name]["preview"] = f"{len(msg[0])} bytes"
                    else:
                        monitor_data[name]["preview"] = ""
                except zmq.Again:
                    continue
        time.sleep(0.01)

# === Tkinter GUI 및 대시보드 구성 ===
root = tk.Tk()
root.title("DepthAI TCP 모니터링 대시보드")
root.geometry("1000x700")

loading_label = tk.Label(root, text="디바이스 연결중...", font=("맑은 고딕", 16))
loading_label.pack(pady=20)

def start_dashboard():
    loading_label.destroy()
    tab_control = ttk.Notebook(root)
    tab_fork = ttk.Frame(tab_control)
    tab_front = ttk.Frame(tab_control)
    tab_rear = ttk.Frame(tab_control)
    tab_control.add(tab_fork, text="포크 카메라")
    tab_control.add(tab_front, text="전면 카메라")
    tab_control.add(tab_rear, text="후면 카메라")
    tab_control.pack(expand=1, fill="both")

    def create_treeview(parent, group_prefix):
        columns = ("스트림", "메시지 건수", "최종 수신 시각", "미리보기", "구독자 수", "발행 상태")
        tree = ttk.Treeview(parent, columns=columns, show="headings")
        for col in columns:
            tree.heading(col, text=col)
            tree.column(col, width=140, anchor="center")
        tree.pack(expand=True, fill="both", padx=10, pady=10)
        for name in sorted(monitor_data.keys()):
            if name.startswith(group_prefix):
                tree.insert("", "end", iid=name, values=(name, 0, "-", "-", "알 수 없음", "-"))
        return tree

    global tree_fork, tree_front, tree_rear
    tree_fork = create_treeview(tab_fork, "fork_")
    tree_front = create_treeview(tab_front, "front_")
    tree_rear = create_treeview(tab_rear, "rear_")

    def show_details(group_prefix):
        details_window = tk.Toplevel(root)
        details_window.title(f"{group_prefix[:-1].upper()} 카메라 상세 정보")
        text = tk.Text(details_window, wrap="word")
        text.pack(expand=True, fill="both")
        details = ""
        for key, data in monitor_data.items():
            if key.startswith(group_prefix):
                details += f"Stream: {key}\n"
                if key in socket_info:
                    for k, v in socket_info[key].items():
                        details += f"  {k}: {v}\n"
                for k, v in data.items():
                    details += f"  {k}: {v}\n"
                details += "\n"
        text.insert("1.0", details)

    btn_details_fork = tk.Button(tab_fork, text="상세 정보 보기", command=lambda: show_details("fork_"))
    btn_details_fork.pack(pady=10)
    btn_details_front = tk.Button(tab_front, text="상세 정보 보기", command=lambda: show_details("front_"))
    btn_details_front.pack(pady=10)
    btn_details_rear = tk.Button(tab_rear, text="상세 정보 보기", command=lambda: show_details("rear_"))
    btn_details_rear.pack(pady=10)

    def update_gui():
        threshold = 2.0
        current_time = time.time()
        for group_prefix, tree in [("fork_", tree_fork), ("front_", tree_front), ("rear_", tree_rear)]:
            for name in monitor_data:
                if name.startswith(group_prefix):
                    data = monitor_data[name]
                    publishing_status = "정지"
                    if (current_time - data["last_timestamp"]) < threshold:
                        publishing_status = "발행중"
                    tree.set(name, "메시지 건수", data["msg_count"])
                    tree.set(name, "최종 수신 시각", data["last_time"] if data["last_time"] else "-")
                    tree.set(name, "미리보기", data["preview"] if data["preview"] else "-")
                    tree.set(name, "구독자 수", data["sub_count"])
                    tree.set(name, "발행 상태", publishing_status)
        root.after(1000, update_gui)

    update_gui()

    title_label = ttk.Label(root, text="DepthAI TCP 모니터링 대시보드", font=("맑은 고딕", 18, "bold"))
    title_label.pack(pady=10)

    listener_thread = threading.Thread(target=zmq_listener, daemon=True)
    listener_thread.start()

    fork_thread = threading.Thread(target=run_fork_pipeline, daemon=True)
    front_thread = threading.Thread(target=run_front_pipeline, daemon=True)
    rear_thread = threading.Thread(target=run_rear_pipeline, daemon=True)
    fork_thread.start()
    front_thread.start()
    rear_thread.start()

def poll_for_devices():
    if initialize_devices():
        start_dashboard()
    else:
        loading_label.config(text="디바이스 연결중...")
        root.after(1000, poll_for_devices)

poll_for_devices()
root.mainloop()
