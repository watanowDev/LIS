import depthai as dai

# 연결된 모든 DepthAI 디바이스 정보 조회
devices = dai.Device.getAllAvailableDevices()

print("연결된 디바이스 수:", len(devices))
if len(devices) != 3:
    print("경고: 예상한 디바이스 수(3개)와 다릅니다!")

# 각 디바이스의 MXID 출력
for idx, device_info in enumerate(devices, start=1): 
    print(f"디바이스 {idx} - MXID: {device_info.getMxId()}")
    print(f"디바이스 {idx} - NAME: {device_info.name}")


front_info = dai.DeviceInfo("192.168.2.101")