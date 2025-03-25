using UnityEngine;

/// <summary>
/// Struct containing information on the width, height, min and max coordinates, and center of a fractal.
/// </summary>
public struct FractalDetails
{
    public float width;
    public float height;
    public Vector3 minPos;
    public Vector3 maxPos;
    public Vector3 centerPoint;
}
public static class FractalSpaceCalculator
{
    /// <summary>
    /// This returns a new FractalDetails struct that contains the screen space details of the bounds of the fractal. The fractal takes up the entire space contained in the bounds, 
    /// but not all of that space may be visible on the screen, so you may want to check that before continuing.
    /// </summary>
    /// <param name="mainCamera">The main camera that renders the scene.</param>
    /// <param name="fractalWorldBounds">The world space bounds of the fractal.</param>
    /// <returns></returns>
    public static FractalDetails CalculateVisible2DFractalScreenSize(Camera mainCamera, Bounds fractalWorldBounds)
    {
        //min position of a rectangle is the lower left corner, where x and y are the lowest they can be
        Vector3 min = fractalWorldBounds.min;
        //max position of a rectangle is the upper right corner, where x and y are the highest they can be
        Vector3 max = fractalWorldBounds.max;

        Vector3 worldBoundsMinPos = new(min.x, min.y, min.z);
        Vector3 worldBoundsMaxPos = new(max.x, max.y, min.z);

        Vector3 screenBoundsMinPos = mainCamera.WorldToScreenPoint(worldBoundsMinPos);
        Vector3 screenBoundsMaxPos = mainCamera.WorldToScreenPoint(worldBoundsMaxPos);

        float screenWidth = screenBoundsMaxPos.x - screenBoundsMinPos.x;
        float screenHeight = screenBoundsMaxPos.y - screenBoundsMinPos.y;

        FractalDetails fractalScreenSpace = new()
        {
            width = screenWidth,
            height = screenHeight,
            minPos = screenBoundsMinPos,
            maxPos = screenBoundsMaxPos
        };

        return fractalScreenSpace;
    }
    /// <summary>
    /// Given a point on the complex number plane (in fractal space), center the fractal around that point by adjusting the min and max values of the real and imaginary parts of the complex number.
    /// </summary>
    /// <param name="minX">Current minimum value of the real part of the complex number (x). </param>
    /// <param name="maxX">Current maximum value of the real part of the complex number (x).</param>
    /// <param name="minY">Current minimum value of the imaginary part of the complex number (y).</param>
    /// <param name="maxY">Current maximum value of the imaginary part of the complex number (y).</param>
    /// <param name="newCenter">The coordinates for the new center point of the fractal.</param>
    public static void CenterFractalAroundPoint(ref float minX, ref float maxX, ref float minY, ref float maxY, Vector3 newCenter)
    {
        float fractalWidth = maxX - minX;
        float fractalHeight = maxY - minY;

        float newMinX = newCenter.x - fractalWidth / 2.0f;
        float newMaxX = newCenter.x + fractalWidth / 2.0f;

        float newMinY = newCenter.y - fractalHeight / 2.0f;
        float newMaxY = newCenter.y + fractalHeight / 2.0f;

        minX = newMinX;
        maxX = newMaxX;
        minY = newMinY;
        maxY = newMaxY;
    }
    /// <summary>
    /// Converts a point from screen space to fractal screen space and then to fractal complex number space coordinates.
    /// </summary>
    /// <param name="screenPointPos">The point to convert, such as the mouse position in the screen./></param>
    /// <param name="mainCamera">The camera that renders the scene.</param>
    /// <param name="fractalWorldBounds">The world space bounds of the fractal.</param>
    /// <param name="minX">The minimum value of x in fractal coordinates (the real part of the complex number).</param>
    /// <param name="maxX">The maximum value of x in fractal coordinates (the real part of the complex number).</param>
    /// <param name="minY">The minimum value of y in fractal coordinates (the imaginary part of the complex number).</param>
    /// <param name="maxY">The maximum value of y in fractal coordinates (the imaginary part of the complex number).</param>
    /// <returns></returns>
    public static FractalDetails ConvertScreenPointToFractalSpace(Vector3 screenPointPos, Camera mainCamera, Bounds fractalWorldBounds, ref float minX, ref float maxX, ref float minY, ref float maxY)
    {
        FractalDetails screenFractalDetails = CalculateVisible2DFractalScreenSize(mainCamera, fractalWorldBounds);

        float xScale = (maxX - minX) / screenFractalDetails.width;
        float yScale = (maxY - minY) / screenFractalDetails.height;

        float scaledScreenX = screenPointPos.x - screenFractalDetails.minPos.x;
        float scaledScreenY = screenPointPos.y - screenFractalDetails.minPos.y;

        float scaledFractalX = minX + scaledScreenX * xScale;
        float scaledFractalY = minY + scaledScreenY * yScale;

        Vector3 fractalPos = new(scaledFractalX, scaledFractalY, fractalWorldBounds.center.z);

        //I did mix and match a bit on the details of this fractal- most are in fractal screen space, but this center is mapped to the complex number plane.
        //Sorry if that's confusing!
        screenFractalDetails.centerPoint = fractalPos;
        CenterFractalAroundPoint(ref minX, ref maxX, ref minY, ref maxY, fractalPos);

        return screenFractalDetails;
    }

}
