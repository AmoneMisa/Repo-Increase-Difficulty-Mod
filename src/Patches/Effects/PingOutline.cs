using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Screen-pixel silhouette and arrow, or a floor ring and arrow. Never fills the target.</summary>
internal sealed class PingOutline : MaskableGraphic
{
    internal PhysGrabObject target;
    internal bool HadTarget;
    internal PlayerAvatar PlayerTarget;
    private bool _hadPlayer;
    internal bool TargetGone => (HadTarget && target == null) || (_hadPlayer && PlayerTarget == null);
    private Renderer[] _renderers;
    private Vector3 _point;
    private float _until;
    private Color _tint;
    private GameObject _root;
    private readonly List<Vector4> _contour = new();
    private Vector2 _objectSize;
    private bool _pointMarker, _pending, _synchronousFallback;
    private float _refreshAt, _arrowU=.5f;
    private float _markerScale=1f;
    private bool _clipLeft,_clipRight,_clipTop,_clipBottom;
    private int _maskWidth, _maskHeight;
    private RenderTexture _mask;
    private Texture2D _readback;
    private static Material _maskMaterial;

    private static readonly int[] Edges = { 0,1, 0,2, 0,4, 1,3, 1,5, 2,3, 2,6, 3,7, 4,5, 4,6, 5,7, 6,7 };

    internal static PingOutline Create(PhysGrabObject target, Vector3 point, Color tint, float duration)
    {
        var root = new GameObject("IHM_PingFrameCanvas", typeof(RectTransform), typeof(Canvas));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35;
        canvas.pixelPerfect = true; // No CanvasScaler: border thickness is in physical screen pixels.
        var go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(root.transform, false);
        var frame = go.AddComponent<PingOutline>();
        frame._root = root;
        frame.target = target;
        frame.HadTarget = target != null;
        frame._renderers = target != null ? target.GetComponentsInChildren<Renderer>(true) : null;
        frame._point = point;
        frame._tint = tint;
        frame._until = Time.time + duration;
        frame.raycastTarget = false;
        frame.maskable = false;
        frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = frame.rectTransform.pivot = Vector2.zero;
        return frame;
    }

    internal static PingOutline CreatePlayer(PlayerAvatar player, Vector3 point, Color tint, float duration)
    {
        var frame = Create(null, point, tint, duration);
        frame.PlayerTarget = player;
        frame._hadPlayer = true;
        var renderers = new List<Renderer>();
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            if (renderer.GetComponentInParent<ItemAttributes>() == null) renderers.Add(renderer);
        frame._renderers = renderers.ToArray();
        return frame;
    }

    internal void Close() { if (_root != null) {_root.SetActive(false);Destroy(_root);} }

    private void LateUpdate()
    {
        if (Time.time >= _until || TargetGone) { Close(); return; }
        var camera = PingController.ActiveCamera();
        if (camera == null) { canvasRenderer.SetAlpha(0f); return; }
        Rect rectangle;
        bool found = false;
        Bounds bounds = default;
        if (_renderers != null)
            foreach (var renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                    !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        _pointMarker=!found;
        if (found)
        {
            if (!ProjectBounds(camera,bounds,out rectangle)) {canvasRenderer.SetAlpha(0f);return;}
            if (!_pending && Time.unscaledTime>=_refreshAt)
            {
                _refreshAt=Time.unscaledTime+.1f;
                CaptureMask(camera,rectangle);
            }
        }
        else
        {
            var screen=HudProjection.WorldToScreen(camera,target!=null?target.centerPoint:PlayerTarget!=null?PlayerTarget.transform.position+Vector3.up*.8f:_point);
            if(screen.z<=camera.nearClipPlane || !HudProjection.DisplayRect(camera).Contains(screen)) {canvasRenderer.SetAlpha(0f);return;}
            _markerScale=Mathf.Clamp(Screen.height/1080f,.8f,2.5f)*ModConfig.PingMarkerScale.Value;
            var size=new Vector2(96f,44f)*_markerScale;
            rectangle=new Rect((Vector2)screen-size*.5f,size);
        }
        _objectSize=rectangle.size;
        rectTransform.anchoredPosition=rectangle.min;
        rectTransform.sizeDelta=rectangle.size+new Vector2(0f,44f*(_pointMarker?_markerScale:1f));
        color=_tint;
        canvasRenderer.SetAlpha(Mathf.Clamp01((_until-Time.time)/.35f));
        SetVerticesDirty();
    }

    // Draw only the selected renderers to a small mask. This works with GPU-only imported meshes,
    // animated/skinned meshes, overlapping parts and concave silhouettes without changing layers.
    internal void CaptureMask(Camera camera,Rect rectangle,bool synchronous=false)
    {
        if(_pending || _renderers==null || rectangle.width<1f || rectangle.height<1f)return;
        if(_maskMaterial==null)
        {
            var shader=Shader.Find("Unlit/Color")??Shader.Find("Standard");
            if(shader==null)return;
            _maskMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            _maskMaterial.SetColor("_Color",Color.white);
            if(_maskMaterial.HasProperty("_MainTex"))_maskMaterial.SetTexture("_MainTex",Texture2D.whiteTexture);
            if(_maskMaterial.HasProperty("_EmissionColor")){_maskMaterial.SetColor("_EmissionColor",Color.white);_maskMaterial.EnableKeyword("_EMISSION");}
        }
        int width=Mathf.Clamp(Mathf.CeilToInt(rectangle.width),16,512);
        int height=Mathf.Clamp(Mathf.CeilToInt(rectangle.height),16,512);
        if(_mask==null || _mask.width!=width || _mask.height!=height)
        {
            if(_mask!=null){_mask.Release();Destroy(_mask);}
            _mask=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Ping silhouette mask",filterMode=FilterMode.Point};_mask.Create();
        }
        var display=HudProjection.DisplayRect(camera);
        _clipLeft=rectangle.xMin<=display.xMin+.5f;_clipRight=rectangle.xMax>=display.xMax-.5f;
        _clipBottom=rectangle.yMin<=display.yMin+.5f;_clipTop=rectangle.yMax>=display.yMax-.5f;
        var crop=Matrix4x4.identity;
        crop.m00=display.width/rectangle.width;crop.m11=display.height/rectangle.height;
        crop.m03=(display.center.x-rectangle.center.x)*2f/rectangle.width;
        crop.m13=(display.center.y-rectangle.center.y)*2f/rectangle.height;
        var commands=new CommandBuffer{name="Ping silhouette"};
        commands.SetRenderTarget(_mask);commands.SetViewport(new Rect(0f,0f,width,height));commands.ClearRenderTarget(true,true,Color.clear);
        // The mask is consumed in bottom-left screen coordinates after readback, not sampled as a camera colour texture.
        commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(crop*camera.projectionMatrix,false));
        foreach(var renderer in _renderers)
            if(renderer!=null && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy &&
                (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                for(int sub=0;sub<renderer.sharedMaterials.Length;sub++)commands.DrawRenderer(renderer,_maskMaterial,sub,0);
        Graphics.ExecuteCommandBuffer(commands);commands.Release();
        _maskWidth=width;_maskHeight=height;
        if(!synchronous && !_synchronousFallback && SystemInfo.supportsAsyncGPUReadback)
        {
            _pending=true;
            AsyncGPUReadback.Request(_mask,0,TextureFormat.RGBA32,request=>
            {
                if(this==null)return;
                _pending=false;
                if(request.hasError)_synchronousFallback=true;
                else BuildContour(request.GetData<Color32>().ToArray(),width,height);
            });
        }
        else
        {
            if(_readback==null || _readback.width!=width || _readback.height!=height)
            {
                if(_readback!=null)Destroy(_readback);
                _readback=new Texture2D(width,height,TextureFormat.RGBA32,false);
            }
            var previous=RenderTexture.active;RenderTexture.active=_mask;
            _readback.ReadPixels(new Rect(0,0,width,height),0,0);_readback.Apply();RenderTexture.active=previous;
            BuildContour(_readback.GetPixels32(),width,height);
        }
    }

    internal void BuildContour(Color32[] pixels,int width,int height)
    {
        _contour.Clear();_maskWidth=width;_maskHeight=height;
        bool Filled(int x,int y)=>x>=0&&y>=0&&x<width&&y<height&&pixels[y*width+x].r>127;
        _arrowU=.5f;
        for(int y=height-1;y>=0;y--)
        {
            int first=-1,last=-1;
            for(int x=0;x<width;x++)if(Filled(x,y)){if(first<0)first=x;last=x;}
            if(first>=0){_arrowU=(first+last+1f)/(2f*width);break;}
        }
        for(int y=0;y<=height;y++)
        {
            if((y==0&&_clipBottom)||(y==height&&_clipTop))continue;
            int start=-1;
            for(int x=0;x<=width;x++)
            {
                bool edge=x<width && Filled(x,y-1)!=Filled(x,y);
                if(edge && start<0)start=x;
                if(!edge && start>=0){_contour.Add(new Vector4(start,y,x,y));start=-1;}
            }
        }
        for(int x=0;x<=width;x++)
        {
            if((x==0&&_clipLeft)||(x==width&&_clipRight))continue;
            int start=-1;
            for(int y=0;y<=height;y++)
            {
                bool edge=y<height && Filled(x-1,y)!=Filled(x,y);
                if(edge && start<0)start=y;
                if(!edge && start>=0){_contour.Add(new Vector4(x,start,x,y));start=-1;}
            }
        }
        SetVerticesDirty();
    }

    internal static bool ProjectBounds(Camera camera, Bounds bounds, out Rect rectangle)
    {
        var corners = new Vector3[8];
        var projected = new Vector3[8];
        float near = camera.nearClipPlane + 0.001f;
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        void Include(Vector3 p) { minX = Mathf.Min(minX,p.x); minY = Mathf.Min(minY,p.y); maxX = Mathf.Max(maxX,p.x); maxY = Mathf.Max(maxY,p.y); }
        for (int i = 0; i < 8; i++)
        {
            corners[i] = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            projected[i] = HudProjection.WorldToScreen(camera,corners[i]);
            if (projected[i].z >= near) Include(projected[i]);
        }
        // Clip edges crossing the near plane instead of projecting behind-camera corners.
        for (int i = 0; i < Edges.Length; i += 2)
        {
            int a = Edges[i], b = Edges[i+1];
            if ((projected[a].z >= near) == (projected[b].z >= near)) continue;
            float t = (near - projected[a].z) / (projected[b].z - projected[a].z);
            Include(HudProjection.WorldToScreen(camera,Vector3.Lerp(corners[a], corners[b], t)));
        }
        var viewport = HudProjection.DisplayRect(camera);
        rectangle = default;
        if (float.IsInfinity(minX) || maxX < viewport.xMin || minX > viewport.xMax || maxY < viewport.yMin || minY > viewport.yMax) return false;
        minX = Mathf.Max(viewport.xMin, minX - 4f); maxX = Mathf.Min(viewport.xMax, maxX + 4f);
        minY = Mathf.Max(viewport.yMin, minY - 4f); maxY = Mathf.Min(viewport.yMax, maxY + 4f);
        rectangle = Rect.MinMaxRect(minX,minY,maxX,maxY);
        return rectangle.width >= 1f && rectangle.height >= 1f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        // Wide low-alpha strokes form a soft halo; the transparent centre is never filled.
        var widths=new[]{16f,11f,7f,4f,2f};
        var alphas=new[]{.025f,.045f,.09f,.19f,.95f};
        for(int layer=0;layer<widths.Length;layer++)
        {
            var tint=color;tint.a*=alphas[layer]*(_pointMarker?1f:.8f);
            float width=widths[layer]*(_pointMarker?_markerScale:1f);
            if(_pointMarker)
            {
                var centre=_objectSize*.5f;const int steps=96;
                var radii=_objectSize*.5f-new Vector2(6f,6f)*_markerScale;
                for(int i=0;i<steps;i++)
                {
                    float a=i*Mathf.PI*2f/steps,b=(i+1)*Mathf.PI*2f/steps;
                    Segment(vh,centre+new Vector2(Mathf.Cos(a)*radii.x,Mathf.Sin(a)*radii.y),
                        centre+new Vector2(Mathf.Cos(b)*radii.x,Mathf.Sin(b)*radii.y),width,tint);
                }
            }
            else if(_maskWidth>0 && _maskHeight>0)
                foreach(var edge in _contour)
                    Segment(vh,new Vector2(edge.x/_maskWidth*_objectSize.x,edge.y/_maskHeight*_objectSize.y),
                        new Vector2(edge.z/_maskWidth*_objectSize.x,edge.w/_maskHeight*_objectSize.y),width,tint);
            Arrow(vh,tint,layer==widths.Length-1?0f:width*.4f);
        }
    }

    private void Arrow(VertexHelper vh,Color tint,float halo)
    {
        float scale=_pointMarker?_markerScale:1f;
        float x=_objectSize.x*(_pointMarker ? .5f : _arrowU),y=_objectSize.y+6f*scale;
        int start=vh.currentVertCount;
        vh.AddVert(new Vector3(x,y-halo),tint,Vector2.zero);
        vh.AddVert(new Vector3(x-10f*scale-halo,y+16f*scale+halo),tint,Vector2.zero);
        vh.AddVert(new Vector3(x+10f*scale+halo,y+16f*scale+halo),tint,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);
    }

    private static void Segment(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
    {
        var d=b-a;if(d.sqrMagnitude<.001f)return;
        var side=new Vector2(-d.y,d.x).normalized*(width*.5f);
        int start=vh.currentVertCount;
        vh.AddVert(a-side,tint,Vector2.zero);vh.AddVert(a+side,tint,Vector2.zero);
        vh.AddVert(b+side,tint,Vector2.zero);vh.AddVert(b-side,tint,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start+2,start+3,start);
    }

    protected override void OnDestroy()
    {
        if(_mask!=null){_mask.Release();Destroy(_mask);}
        if(_readback!=null)Destroy(_readback);
        base.OnDestroy();
    }
}
