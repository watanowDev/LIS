import depthai as dai
import cv2
import threading
import time
import blobconverter

# 설정된 IP 주소로 연결
fork_info = dai.DeviceInfo("192.168.1.101")
front_info = dai.DeviceInfo("192.168.2.101")
rear_info = dai.DeviceInfo("2.1.4")

# 모델 파일 경로 설정 함수
def get_model_path(model_name="yolov4_tiny_coco_416x416"):
    try:
        # 모델 파일 다운로드 또는 캐시에서 가져오기
        model_path = blobconverter.from_zoo(
            model_name,
            shaves=4,  # 4개의 코어로 컴파일
            zoo_type="depthai",
            use_cache=True
        )
        print(f"Model downloaded or found in cache: {model_path}")
        return model_path
    except Exception as e:
        print(f"Error downloading model: {e}")
        return None

# 파이프라인 생성 함수
def create_pipeline(W=416, H=416, nnPath="", confidenceThreshold=0.5, classes=80, coordinates=4, anchors=[], anchorMasks={}, iouThreshold=0.5):
    pipeline = dai.Pipeline()

    # RGB 카메라 노드 생성
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

    # 스트림 이름 설정
    xoutRgb.setStreamName("rgb")
    nnOut.setStreamName("nn")
    xoutDepth.setStreamName("depth")
    xoutLeft.setStreamName("left")
    xoutRight.setStreamName("right")
    xoutVideo.setStreamName("video")

    # RGB 카메라 설정
    camRgb.setPreviewSize(W, H)  # 네트워크 입력 크기와 일치
    camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_1080_P)  # 지원되는 해상도로 변경
    camRgb.setInterleaved(False)
    camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
    camRgb.setFps(10)

    # Mono 카메라 설정
    monoLeft.setBoardSocket(dai.CameraBoardSocket.CAM_B)
    monoLeft.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
    monoLeft.setFps(10)
    monoRight.setBoardSocket(dai.CameraBoardSocket.CAM_C)
    monoRight.setResolution(dai.MonoCameraProperties.SensorResolution.THE_720_P)
    monoRight.setFps(10)

    # StereoDepth 설정
    stereo.setDefaultProfilePreset(dai.node.StereoDepth.PresetMode.ROBOTICS)
    stereo.initialConfig.setConfidenceThreshold(200)
    stereo.setLeftRightCheck(True)
    stereo.setExtendedDisparity(False)
    stereo.setSubpixel(False)
    stereo.setDepthAlign(dai.CameraBoardSocket.CAM_A)
    stereo.setOutputSize(W, H)

    # YoloDetectionNetwork 설정
    detectionNetwork.setConfidenceThreshold(confidenceThreshold)
    detectionNetwork.setNumClasses(classes)
    detectionNetwork.setCoordinateSize(coordinates)
    detectionNetwork.setAnchors(anchors)
    detectionNetwork.setAnchorMasks(anchorMasks)
    detectionNetwork.setIouThreshold(iouThreshold)
    detectionNetwork.setBlobPath(nnPath)
    detectionNetwork.setNumInferenceThreads(2)
    detectionNetwork.input.setBlocking(False)

    # 노드 연결
    camRgb.preview.link(detectionNetwork.input)
    detectionNetwork.passthrough.link(xoutRgb.input)
    detectionNetwork.out.link(nnOut.input)
    monoLeft.out.link(stereo.left)
    monoRight.out.link(stereo.right)
    stereo.depth.link(xoutDepth.input)
    monoLeft.out.link(xoutLeft.input)
    monoRight.out.link(xoutRight.input)
    camRgb.video.link(xoutVideo.input)

    return pipeline

# 카메라 테스트 함수
def test_camera(name, device_info, pipeline):
    print(f"Connecting to {name} camera device...")
    try:
        with dai.Device(pipeline, devInfo=device_info) as device:
            print(f"{name} camera device connected successfully.")

            # RGB 출력 큐 생성
            qRgb = device.getOutputQueue(name="rgb", maxSize=4, blocking=False)

            # FPS 계산을 위한 변수 초기화
            frame_count = 0
            start_time = time.time()
            fps = 0

            print(f"Starting {name} camera preview...")
            while True:
                inRgb = qRgb.tryGet()  # RGB 프레임 가져오기
                if inRgb is not None:
                    # 프레임을 numpy 배열로 변환
                    frame = inRgb.getCvFrame()

                    # FPS 계산
                    frame_count += 1
                    current_time = time.time()
                    elapsed_time = current_time - start_time
                    if elapsed_time > 1:  # 1초마다 FPS 계산
                        fps = frame_count / elapsed_time
                        frame_count = 0
                        start_time = current_time

                    # FPS를 프레임에 표시
                    cv2.putText(frame, f"FPS: {fps:.2f}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)

                    # 화면에 표시
                    cv2.imshow(f"{name} Camera - RGB", frame)

                # 'q' 키를 누르면 종료
                if cv2.waitKey(1) == ord('q'):
                    print(f"Exiting {name} camera test...")
                    break

    except Exception as e:
        print(f"Error while testing {name} camera: {e}")

    finally:
        cv2.destroyAllWindows()

# 멀티쓰레드 실행 함수
def run_all_cameras():
    # 모델 파일 경로 가져오기
    nnPath = get_model_path("yolov4_tiny_coco_416x416")
    if nnPath is None:
        raise RuntimeError("Failed to download or locate the model blob file.")

    # 파이프라인 생성
    pipeline = create_pipeline(
        W=1280,
        H=720,
        nnPath=nnPath,
        confidenceThreshold=0.5,
        classes=80,
        coordinates=4,
        anchors=[10, 14, 23, 27, 37, 58, 81, 82, 135, 169, 344, 319],
        anchorMasks={"side26": [1, 2, 3], "side13": [3, 4, 5]},
        iouThreshold=0.5
    )

    # 각 카메라를 별도의 쓰레드에서 실행
    fork_thread = threading.Thread(target=test_camera, args=("Fork", fork_info, pipeline))
    front_thread = threading.Thread(target=test_camera, args=("Front", front_info, pipeline))
    rear_thread = threading.Thread(target=test_camera, args=("Rear", rear_info, pipeline))

    # 쓰레드 시작
    fork_thread.start()
    front_thread.start()
    rear_thread.start()

    # 모든 쓰레드가 종료될 때까지 대기
    fork_thread.join()
    front_thread.join()
    rear_thread.join()

# 메인 실행
if __name__ == "__main__":
    run_all_cameras()