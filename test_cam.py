import cv2

for i in range(2):
    cap = cv2.VideoCapture(i)
    if cap.isOpened():
        ret, frame = cap.read()
        if ret:
            cv2.imwrite(f'cam_{i}.jpg', frame)
        cap.release()
