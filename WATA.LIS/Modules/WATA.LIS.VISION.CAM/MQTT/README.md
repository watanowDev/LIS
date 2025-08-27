# StreamingServer Luxonis

DepthAI를 사용한 실시간 스트리밍 서버입니다.

## 주요 기능

- 멀티 카메라 지원 (Fork, Front, Rear)
- YOLO 객체 탐지
- 실시간 깊이 정보 처리
- ZeroMQ를 통한 데이터 스트리밍
- ROI(Region of Interest) 기반 깊이 측정

## 파일 구조

- `StreamingServer.py`: 메인 스트리밍 서버
- `Recorder.py`: 레코딩 기능
- `DetectionRcv.cs`: C# 탐지 수신기
- `json/`: YOLO 모델 설정 파일들

## 사용법

```bash
python StreamingServer.py
```

## 요구사항

- Python 3.7+
- DepthAI
- OpenCV
- ZeroMQ
- NumPy

## 설치

```bash
pip install depthai opencv-python zmq numpy requests
```
