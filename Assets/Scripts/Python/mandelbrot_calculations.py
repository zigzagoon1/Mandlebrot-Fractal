import zmq
import mpmath
import json

mpmath.mp.dps = 300

def mandelbrot(c, max_iter):
    z = mpmath.mpc(0, 0)
    for n in range(max_iter):
        if abs(z) > 2:
            return n
        z = z * z + c
    return max_iter

def calculate_bounds(min_re, max_re, min_im, max_im, zoom_level):
    #Calculate current center
    center_re = (min.re + max_re) / 2
    center_im = (min_im + max_im) / 2
    
    width = max_re - min_re
    height = max_im - min_im
    
    #Calculate new width and height based on zoom level
    new_width = width / zoom_level
    new_height = height / zoom_level
    
    #Calculate new min and max bounds
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
        
        if "terminate" in message and message["terminate"]:
            print("Termination signal received. Exiting...", flush=True)
            break
        #min_re = mpmath.mpf(message['min_re'])
        #max_re = mpmath.mpf(message['max_re'])
        #min_im = mpmath.mpf(message['min_im'])
        #max_im = mpmath.mpf(message['max_im'])
        #zoom_level = mpmath.mpf(message['zoom_level'])
        
        #new_min_re, new_max_re, new_min_im, new_max_im = calculate_bounds(min_re, max_re, min_im, max_im, zoom_level)
        
        #response = {
            #"new_min_re": str(new_min_re),
            #"new_max_re": str(new_max_re),
            #"new_min_im": str(new_min_im),
            #"new_max_im": str(new_max_im)
        #}
        
        
        print(message, flush=True)
        hello_world = 'Hello World'
        socket.send_json(hello_world)


if __name__ == "__main__":
    main()