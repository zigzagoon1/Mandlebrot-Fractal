# Mandlebrot

A work-in-progress and curiosity-driven project for exploring the Mandelbrot fractal.

Currently, you can zoom in and out on different areas of the fractal by clicking at a desired center point and using the scroll wheel on your mouse to control the zoom. 
Interestingly, you may notice that as you scroll into the fractal more and more, the details begin to become blocky and pixelated. This is due to limitations in the 
precision of floating-point numbers, which are used in the calculations of the fractal. Double precision would allow for more zoom, but even that is not very good, 
and to use them with shaders is a pain as they, and GPU's in general, are optimized for floats. I plan to explore other methods of calculating the positions for the 
fractal in order to allow for a far greater zoom ability. 
