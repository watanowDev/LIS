import depthai as dai

info = dai.DeviceInfo("192.168.1.101")
print(info)

fork_pipeline = dai.Pipeline()

# Connect to device and start pipeline
with dai.Device(fork_pipeline, devInfo=info) as device:
    deviceInfo = device.getDeviceInfo()
    print("Device info: ")
    print(deviceInfo)
    # Print out usb speed
    print('Usb speed:', device.getUsbSpeed().name)