import depthai as dai
import numpy as np

def print_calibration(calib, board_socket, camera_name):
    # 읽은 intrinsics (3x3 카메라 행렬)
    intrinsics = calib.getCameraIntrinsics(board_socket)
    if intrinsics is None:
        print(f"{camera_name} intrinsics not available.")
    else:
        cameraMatrix = np.array(intrinsics).reshape((3, 3))
        print(f"{camera_name} Camera Matrix:")
        print(cameraMatrix)
    
    # 왜곡 계수 (1차원 배열로 reshape)
    distCoeffs = calib.getDistortionCoefficients(board_socket)
    if distCoeffs is None:
        print(f"{camera_name} distortion coefficients not available.")
    else:
        distCoeffs = np.array(distCoeffs).flatten()
        print(f"{camera_name} Distortion Coefficients:")
        print(distCoeffs)

# 모든 USB 연결된 장치 검색
devices = dai.Device.getAllAvailableDevices()
print("Available devices:")
for d in devices:
    print(d.getMxId())

# 예시: front와 rear 카메라의 MxID로 구분하여 캘리브레이션 데이터 출력
front_mxid = "19443010C180962E00"
rear_mxid  = "19443010C142962E00"

for d in devices:
    if d.getMxId() == front_mxid:
        print("===== Front Camera Calibration =====")
        with dai.Device(d) as device:
            calib = device.readCalibration()
            print_calibration(calib, dai.CameraBoardSocket.CAM_B, "Front Camera")
    elif d.getMxId() == rear_mxid:
        print("===== Rear Camera Calibration =====")
        with dai.Device(d) as device:
            calib = device.readCalibration()
            print_calibration(calib, dai.CameraBoardSocket.CAM_A, "Rear Camera")
