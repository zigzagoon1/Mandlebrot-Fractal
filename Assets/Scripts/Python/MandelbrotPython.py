import mpmath
import zmq
import json

mpmath.mp.dps = 300  # Set precision to 300 decimal places

def mandelbrot(c, max_iter):
    z = mpmath.mpc(0, 0)
    for n in range(max_iter):
        if abs(z) > 2:
            return n
        z = z * z + c
    return max_iter

def calculate_bounds(min_re, max_re, min_im, max_im, zoom_level):
    center_re = (min_re + max_re) / 2
    center_im = (min_im + max_im) / 2
    width = max_re - min_re
    height = max_im - min_im
    new_width = width / zoom_level
    new_height = height / zoom_level
    new_min_re = center_re - new_width / 2
    new_max_re = center_re + new_width / 2
    new_min_im = center_im - new_height / 2
    new_max_im = center_im + new_height / 2
    return new_min_re, new_max_re, new_min_im, new_max_im

def main():
    context = zmq.Context()
    socket = context.socket(zmq.REP)
    socket.bind("tcp://*:5555")

    while True:
        message = socket.recv_json()
        message = json.loads(message)
        
        if message.get("terminate"):
            print ("Termination signal received. Exiting...")
            break
        
        min_re = mpmath.mpf(message['min_re'])
        max_re = mpmath.mpf(message['max_re'])
        min_im = mpmath.mpf(message['min_im'])
        max_im = mpmath.mpf(message['max_im'])
        zoom_level = mpmath.mpf(message['zoom_level'])
        
        new_min_re, new_max_re, new_min_im, new_max_im = calculate_bounds(min_re, max_re, min_im, max_im, zoom_level)
        
        response = {
            "new_min_re": str(new_min_re),
            "new_max_re": str(new_max_re),
            "new_min_im": str(new_min_im),
            "new_max_im": str(new_max_im)
        }
        socket.send_json(response)

if __name__ == "__main__":
    main()
