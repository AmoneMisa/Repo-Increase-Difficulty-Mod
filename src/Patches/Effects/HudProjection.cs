using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Maps the game's low-resolution camera output to the rectangle actually displayed on screen.</summary>
internal static class HudProjection
{
    internal static Camera CanvasCamera(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        if (canvas.worldCamera != null) return canvas.worldCamera;
        if (canvas.rootCanvas != null && canvas.rootCanvas.worldCamera != null) return canvas.rootCanvas.worldCamera;
        return CameraOverlay.instance != null ? CameraOverlay.instance.overlayCamera : null;
    }

    internal static Rect DisplayRect(Camera camera)
    {
        if (camera == null) return new Rect(0f,0f,Screen.width,Screen.height);
        var main = RenderTextureMain.instance;
        var image = main != null ? main.GetComponent<RawImage>() : null;
        if (image != null && image.isActiveAndEnabled && image.canvas != null)
        {
            var corners=new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
            var outputCamera=CanvasCamera(image.canvas);
            var min=RectTransformUtility.WorldToScreenPoint(outputCamera,corners[0]);
            var max=RectTransformUtility.WorldToScreenPoint(outputCamera,corners[2]);
            if(max.x>min.x && max.y>min.y) return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        if(camera.targetTexture==null) return camera.pixelRect;
        // Fallback during scene initialization, before the game's display RawImage is ready.
        float aspect=camera.targetTexture.width/(float)camera.targetTexture.height;
        float width=Screen.width, height=width/aspect;
        if(height>Screen.height) {height=Screen.height;width=height*aspect;}
        return new Rect((Screen.width-width)*.5f,(Screen.height-height)*.5f,width,height);
    }

    internal static Vector3 WorldToScreen(Camera camera,Vector3 world)
    {
        if(camera==null) return RectTransformUtility.WorldToScreenPoint(null,world);
        var viewport=camera.WorldToViewportPoint(world);
        var display=DisplayRect(camera);
        return new Vector3(display.x+viewport.x*display.width,display.y+viewport.y*display.height,viewport.z);
    }
}
