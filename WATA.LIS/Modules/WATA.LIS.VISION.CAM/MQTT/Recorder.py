import cv2
import depthai as dai
import numpy as np
import time
import os
import threading
from datetime import datetime
import sys

# 녹화 상태 관리
recording = False
writer = None
output_filename = None
frame_count = 0
fps = 10
max_file_size_mb = 100  # 최대 파일 크기 (MB)
current_file_size_mb = 0
file_sequence = 1  # 파일 시퀀스 번호

# 프로그램 시작 시간
program_start_time = time.time()

# 키보드 입력을 비차단적으로 처리하기 위한 함수
def keyboard_listener():
    global recording, writer, output_filename, frame_count
    
    print("=" * 60)
    print("RGB Recording Program")
    print("=" * 60)
    print("Commands:")
    print("  's' - Start recording")
    print("  'e' - Stop recording")
    print("  'q' - Quit program")
    print("=" * 60)
    
    while True:
        try:
            key = input().strip().lower()
            
            if key == 's':
                if not recording:
                    start_recording()
                else:
                    print("Already recording!")
                    
            elif key == 'e':
                if recording:
                    stop_recording()
                else:
                    print("Not recording!")
                    
            elif key == 'q':
                if recording:
                    stop_recording()
                print("Quitting program...")
                os._exit(0)
                
        except KeyboardInterrupt:
            if recording:
                stop_recording()
            print("\nProgram terminated.")
            os._exit(0)

def start_recording():
    global recording, writer, output_filename, frame_count, file_sequence, current_file_size_mb
    
    # 타임스탬프를 포함한 파일명 생성
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_filename = f"recording_{timestamp}_part{file_sequence:03d}.mp4"
    
    # VideoWriter 설정 (1280x720 해상도, 10 FPS)
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    writer = cv2.VideoWriter(output_filename, fourcc, fps, (1280, 720))
    
    recording = True
    frame_count = 0
    current_file_size_mb = 0
    
    print(f"[{datetime.now().strftime('%H:%M:%S')}] Recording started: {output_filename}")

def create_new_recording_file():
    """현재 녹화 파일을 닫고 새로운 파일로 녹화를 계속"""
    global writer, output_filename, frame_count, file_sequence, current_file_size_mb
    
    if writer is not None:
        writer.release()
        
        # 이전 파일 정보 출력
        if os.path.exists(output_filename):
            file_size = os.path.getsize(output_filename) / (1024 * 1024)
            print(f"[{datetime.now().strftime('%H:%M:%S')}] File completed: {output_filename} ({file_size:.2f} MB, {frame_count} frames)")
    
    # 새 파일 시작
    file_sequence += 1
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_filename = f"recording_{timestamp}_part{file_sequence:03d}.mp4"
    
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    writer = cv2.VideoWriter(output_filename, fourcc, fps, (1280, 720))
    
    frame_count = 0
    current_file_size_mb = 0
    
    print(f"[{datetime.now().strftime('%H:%M:%S')}] New recording file started: {output_filename}")

def check_file_size():
    """현재 녹화 파일의 크기를 확인하고 필요시 새 파일 생성"""
    global current_file_size_mb, output_filename
    
    if output_filename and os.path.exists(output_filename):
        current_file_size_mb = os.path.getsize(output_filename) / (1024 * 1024)
        
        if current_file_size_mb >= max_file_size_mb:
            create_new_recording_file()
            return True
    return False

def stop_recording():
    global recording, writer, output_filename, frame_count, file_sequence, current_file_size_mb
    
    if writer is not None:
        writer.release()
        writer = None
    
    recording = False
    
    print(f"[{datetime.now().strftime('%H:%M:%S')}] Recording stopped: {output_filename}")
    print(f"Total frames recorded in last file: {frame_count}")
    print(f"Total files created: {file_sequence}")
    
    # 파일 크기 확인
    if output_filename and os.path.exists(output_filename):
        file_size = os.path.getsize(output_filename) / (1024 * 1024)  # MB 단위
        print(f"Last file size: {file_size:.2f} MB")
    
    # 시퀀스 번호 초기화
    file_sequence = 1
    current_file_size_mb = 0

def main():
    global recording, writer, frame_count, current_file_size_mb
    
    # 스크립트 디렉토리로 작업 디렉토리 변경
    script_dir = os.path.dirname(os.path.abspath(__file__))
    os.chdir(script_dir)
    
    print(f"Working directory: {os.getcwd()}")
    print(f"Max file size per recording: {max_file_size_mb} MB")
    
    # 키보드 입력 리스너 쓰레드 시작
    keyboard_thread = threading.Thread(target=keyboard_listener, daemon=True)
    keyboard_thread.start()
    
    # DepthAI 장치 정보 설정 (StreamingServer.py에서 가져온 정보)
    device_info = dai.DeviceInfo("169.254.1.222")
    
    # 파이프라인 생성
    pipeline = dai.Pipeline()
    
    # RGB 카메라 노드 생성
    camRgb = pipeline.create(dai.node.ColorCamera)
    xoutVideo = pipeline.create(dai.node.XLinkOut)
    
    xoutVideo.setStreamName("video")
    
    # RGB 카메라 설정
    camRgb.setResolution(dai.ColorCameraProperties.SensorResolution.THE_720_P)
    camRgb.setInterleaved(False)
    camRgb.setColorOrder(dai.ColorCameraProperties.ColorOrder.BGR)
    camRgb.setFps(fps)
    
    # 링킹
    camRgb.video.link(xoutVideo.input)
    
    try:
        # 장치 연결
        with dai.Device(pipeline, devInfo=device_info, usb2Mode=False) as device:
            print(f"Connected to device: {device.getDeviceName()}")
            
            qVideo = device.getOutputQueue(name="video", maxSize=4, blocking=False)
            
            # FPS 계산용 변수
            prevTime = time.monotonic()
            startTime = time.monotonic()
            frame_counter = 0
            
            print("Camera initialized. Waiting for commands...")
            
            while True:
                inVideo = qVideo.get()
                
                if inVideo is not None:
                    frame = inVideo.getCvFrame()
                    frame_counter += 1
                    
                    # FPS 계산
                    currentTime = time.monotonic()
                    display_fps = 1 / (currentTime - prevTime) if (currentTime - prevTime) > 0 else 0
                    prevTime = currentTime
                    
                    # 평균 FPS 계산
                    avg_fps = frame_counter / (currentTime - startTime) if (currentTime - startTime) > 0 else 0
                    
                    # 녹화 중인 경우 프레임 저장
                    if recording and writer is not None:
                        writer.write(frame)
                        frame_count += 1
                        
                        # 10프레임마다 파일 크기 체크 (성능 최적화)
                        if frame_count % 10 == 0:
                            check_file_size()
                    
                    # 상태 정보 오버레이
                    status_color = (0, 0, 255) if recording else (0, 255, 0)
                    status_text = "RECORDING" if recording else "STANDBY"
                    
                    cv2.putText(frame, f"Status: {status_text}", (10, 30), 
                               cv2.FONT_HERSHEY_SIMPLEX, 0.7, status_color, 2)
                    cv2.putText(frame, f"FPS: {display_fps:.1f}", (10, 60), 
                               cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 1)
                    cv2.putText(frame, f"Avg FPS: {avg_fps:.1f}", (10, 80), 
                               cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 1)
                    
                    if recording:
                        cv2.putText(frame, f"Frames: {frame_count}", (10, 100), 
                                   cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 255), 1)
                        cv2.putText(frame, f"File: Part {file_sequence:03d}", (10, 120), 
                                   cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 255), 1)
                        cv2.putText(frame, f"Size: {current_file_size_mb:.1f}/{max_file_size_mb} MB", (10, 140), 
                                   cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 255), 1)
                        # 녹화 중임을 표시하는 빨간 점
                        cv2.circle(frame, (frame.shape[1] - 30, 30), 10, (0, 0, 255), -1)
                    
                    # 시간 정보 표시
                    current_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
                    cv2.putText(frame, current_time, (10, frame.shape[0] - 20), 
                               cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1)
                    
                    # 프레임 표시
                    cv2.imshow("RGB Recorder", frame)
                    cv2.setWindowProperty("RGB Recorder", cv2.WND_PROP_TOPMOST, 1)
                
                # OpenCV 창에서 키 입력 처리 (ESC 키로 종료)
                key = cv2.waitKey(1) & 0xFF
                if key == 27:  # ESC 키
                    break
                elif key == ord('s'):  # 's' 키로 녹화 시작
                    if not recording:
                        start_recording()
                elif key == ord('e'):  # 'e' 키로 녹화 중지
                    if recording:
                        stop_recording()
                        
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
    finally:
        # 정리 작업
        if recording and writer is not None:
            stop_recording()
        cv2.destroyAllWindows()
        print("Program ended.")

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nProgram interrupted by user.")
        sys.exit(0)
