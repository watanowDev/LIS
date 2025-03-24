#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
DepthAI TCP 모니터링 대시보드
 - 각 카메라 그룹(포크, 전면, 후면)의 PUB 스트림(예: rgb, left, right, depth, detection, video)을
   구독하여, 1초마다 수신된 메시지 건수, 마지막 수신 시각, 메시지 미리보기 등을 표시합니다.
 - PUB 소켓은 구독자 수(발행 상태)를 직접 확인할 수 없으므로 해당 항목은 “알 수 없음”으로 표시됩니다.
"""

import zmq
import threading
import time
import numpy as np
import tkinter as tk
from tkinter import ttk
from datetime import datetime

# --- 설정 ---
# 각 그룹별로 사용될 ZeroMQ PUB 엔드포인트 (예시)
endpoints = {
    # 포크(Fork) 카메라 (5560~5565)
    "fork_rgb":       "tcp://localhost:5560",
    "fork_left":      "tcp://localhost:5561",
    "fork_right":     "tcp://localhost:5562",
    "fork_depth":     "tcp://localhost:5563",
    "fork_detection": "tcp://localhost:5564",
    "fork_video":     "tcp://localhost:5565",
    # 전면(Front) 카메라 (5570~5575)
    "front_rgb":       "tcp://localhost:5570",
    "front_left":      "tcp://localhost:5571",
    "front_right":     "tcp://localhost:5572",
    "front_depth":     "tcp://localhost:5573",
    "front_detection": "tcp://localhost:5574",
    "front_video":     "tcp://localhost:5575",
    # 후면(Rear) 카메라 (5580~5585)
    "rear_rgb":       "tcp://localhost:5580",
    "rear_left":      "tcp://localhost:5581",
    "rear_right":     "tcp://localhost:5582",
    "rear_depth":     "tcp://localhost:5583",
    "rear_detection": "tcp://localhost:5584",
    "rear_video":     "tcp://localhost:5585",
}

# --- 모니터링 대시보드 관련 함수 ---
def create_dashboard(width=900, height=600):
    """지정된 크기의 흰색 배경 이미지(대시보드)를 생성합니다."""
    return 255 * np.ones((height, width, 3), dtype=np.uint8)

def draw_dashboard(image, title, info_lines):
    """이미지에 제목과 정보(문자열 리스트)를 그립니다."""
    image[:] = 255  # 흰색 배경
    font = cv2.FONT_HERSHEY_SIMPLEX
    title_scale, text_scale = 1.5, 1.0
    thickness_title, thickness_text = 2, 1
    color_title, color_text = (0, 0, 0), (80, 80, 80)
    margin, line_spacing = 20, 35

    # 제목 중앙 정렬
    (title_w, title_h), _ = cv2.getTextSize(title, font, title_scale, thickness_title)
    title_x = (image.shape[1] - title_w) // 2
    title_y = margin + title_h
    cv2.putText(image, title, (title_x, title_y), font, title_scale, color_title, thickness_title, cv2.LINE_AA)
    # 제목 아래 구분선 그리기
    cv2.line(image, (margin, title_y + 10), (image.shape[1] - margin, title_y + 10), (0, 0, 0), 2)
    
    # 정보 텍스트 출력
    y0 = title_y + 40
    for i, line in enumerate(info_lines):
        y = y0 + i * line_spacing
        cv2.putText(image, line, (margin, y), font, text_scale, color_text, thickness_text, cv2.LINE_AA)
    return image

# --- ZeroMQ 소켓 초기화 ---
context = zmq.Context()
sockets = {}
monitor_data = {}

# 각 엔드포인트에 대해 SUB 소켓 생성, 연결 및 모든 토픽 구독
for name, endpoint in endpoints.items():
    sock = context.socket(zmq.SUB)
    sock.connect(endpoint)
    sock.setsockopt_string(zmq.SUBSCRIBE, "")
    sockets[name] = sock
    monitor_data[name] = {
        "msg_count": 0,
        "last_time": "",
        "preview": "",
        "sub_count": "알 수 없음"  # PUB 소켓은 구독자 수를 알 수 없음
    }

# Poller에 모든 소켓 등록
poller = zmq.Poller()
for sock in sockets.values():
    poller.register(sock, zmq.POLLIN)

# --- ZeroMQ 메시지 리스너 스레드 ---
def zmq_listener():
    global monitor_data
    while True:
        events = dict(poller.poll(100))  # 100ms 폴링
        for name, sock in sockets.items():
            if sock in events and events[sock] == zmq.POLLIN:
                try:
                    msg = sock.recv_multipart(flags=zmq.NOBLOCK)
                    monitor_data[name]["msg_count"] += 1
                    monitor_data[name]["last_time"] = datetime.now().strftime("%H:%M:%S")
                    if msg and len(msg) > 0:
                        monitor_data[name]["preview"] = f"{len(msg[0])} bytes"
                    else:
                        monitor_data[name]["preview"] = ""
                except zmq.Again:
                    continue
        time.sleep(0.01)

listener_thread = threading.Thread(target=zmq_listener, daemon=True)
listener_thread.start()

# --- Tkinter GUI (대시보드) ---
import cv2  # cv2 사용
import tkinter as tk
from tkinter import ttk

root = tk.Tk()
root.title("DepthAI TCP 모니터링 대시보드")
root.geometry("1000x700")

# Notebook 탭 생성 (그룹별: 포크, 전면, 후면)
tab_control = ttk.Notebook(root)
tab_fork = ttk.Frame(tab_control)
tab_front = ttk.Frame(tab_control)
tab_rear = ttk.Frame(tab_control)
tab_control.add(tab_fork, text="포크 카메라")
tab_control.add(tab_front, text="전면 카메라")
tab_control.add(tab_rear, text="후면 카메라")
tab_control.pack(expand=1, fill="both")

# 그룹별 Treeview 테이블 생성 함수 (컬럼 헤더를 한글로)
def create_treeview(parent, group_prefix):
    columns = ("스트림", "메시지 건수", "최종 수신 시각", "미리보기", "구독자 수")
    tree = ttk.Treeview(parent, columns=columns, show="headings")
    for col in columns:
        tree.heading(col, text=col)
        tree.column(col, width=140, anchor="center")
    tree.pack(expand=True, fill="both", padx=10, pady=10)
    # 해당 그룹에 속하는 스트림만 추가
    for name in sorted(monitor_data.keys()):
        if name.startswith(group_prefix):
            tree.insert("", "end", iid=name, values=(name, 0, "-", "-", "알 수 없음"))
    return tree

tree_fork = create_treeview(tab_fork, "fork_")
tree_front = create_treeview(tab_front, "front_")
tree_rear = create_treeview(tab_rear, "rear_")

# GUI 업데이트 함수 (1초마다 호출)
def update_gui():
    for group_prefix, tree in [("fork_", tree_fork), ("front_", tree_front), ("rear_", tree_rear)]:
        for name in monitor_data:
            if name.startswith(group_prefix):
                data = monitor_data[name]
                tree.set(name, "메시지 건수", data["msg_count"])
                tree.set(name, "최종 수신 시각", data["last_time"] if data["last_time"] != "" else "-")
                tree.set(name, "미리보기", data["preview"] if data["preview"] != "" else "-")
                tree.set(name, "구독자 수", data["sub_count"])
    root.after(1000, update_gui)

update_gui()

# 제목 라벨
title_label = ttk.Label(root, text="DepthAI TCP 모니터링 대시보드", font=("맑은 고딕", 18, "bold"))
title_label.pack(pady=10)

root.mainloop()
