using System;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    /// <summary>
    /// Tileable procedural surface tiles. RGB is a tone multiplier for the material colour with relief
    /// shading baked in; alpha masks smoothness, so joints, pores and cracks stay matte.
    /// </summary>
    public static class SurfaceTextures
    {
        public const int Size=128;
        public static readonly string[] Patterns={ "plain","planks","blocks","mottle","pores","cracks","bark","weave","leaves","vista","sky" };

        public static void Fill(Texture2D texture,string pattern,float amount,int seed)
        {
            if(pattern=="vista" || pattern=="sky") { Vista(texture,seed,pattern=="vista"); return; }
            var height=new float[Size*Size]; var tone=new float[Size*Size]; var gloss=new float[Size*Size];
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
            {
                float u=x/(float)Size,v=y/(float)Size; float h=.5f,t=1,g=1;
                switch(pattern)
                {
                    case "planks":
                    {
                        int board=Mathf.FloorToInt(u*8); float across=u*8-board,along=Frac(v+Hash(board,3,seed));
                        bool gap=across<.05f || across>.95f || along<.012f;
                        float grain=Fbm(u*48,v*3+board*.37f,48,3,seed);
                        t=(.8f+.25f*Hash(board,7,seed))*(.88f+.2f*grain); h=gap?0:.6f+.4f*grain; g=gap?.15f:.6f+.4f*grain; if(gap) t*=.5f;
                        break;
                    }
                    case "blocks":
                    {
                        int row=Mathf.FloorToInt(v*4); float shifted=Frac(u+(row%2)*.25f); int col=Mathf.FloorToInt(shifted*2);
                        float fu=shifted*2-col,fv=v*4-row;
                        float edge=Mathf.Min(Mathf.Min(fu,1-fu)*2,Mathf.Min(fv,1-fv));
                        bool mortar=edge<.06f; float noise=Fbm(u*10,v*10,10,4,seed);
                        t=(.82f+.24f*Hash(col+row*5,11,seed))*(.86f+.24f*noise); h=mortar?0:Mathf.SmoothStep(.4f,1,edge*5)*.7f+.3f*noise;
                        g=mortar?.1f:.55f+.45f*noise; if(mortar) t*=.6f;
                        break;
                    }
                    case "mottle":
                    {
                        float big=Fbm(u*4,v*4,4,4,seed),fine=Fbm(u*24,v*24,24,2,seed+5);
                        t=Mathf.Lerp(.68f,1.14f,big)*(.9f+.2f*fine); h=.5f*big+.5f*fine; g=.3f+.7f*fine;
                        break;
                    }
                    case "pores":
                    {
                        float noise=Fbm(u*6,v*6,6,4,seed); Cells(u,v,14,seed,out float near,out _,out _);
                        bool pit=near<.13f; t=(.84f+.24f*noise)*(pit?.8f:1); h=pit?.1f:.6f+.4f*noise; g=pit?.1f:.5f+.5f*noise;
                        break;
                    }
                    case "cracks":
                    {
                        Cells(u,v,5,seed,out float near,out float second,out _); float noise=Fbm(u*8,v*8,8,3,seed);
                        bool crack=second-near<.035f; t=crack?.66f:.95f+.08f*noise; h=crack?0:.8f+.2f*noise; g=crack?.2f:.92f+.08f*noise;
                        break;
                    }
                    case "bark":
                    {
                        float warp=Fbm(u*3,v*1.5f,3,3,seed)*2.6f; float ridge=Mathf.Abs(Mathf.Sin((u*10+warp)*Mathf.PI));
                        float noise=Fbm(u*20,v*6,20,2,seed+3);
                        t=Mathf.Lerp(.55f,1.08f,Mathf.Pow(ridge,.7f))*(.88f+.2f*noise); h=ridge; g=.2f+.6f*ridge;
                        break;
                    }
                    case "weave":
                    {
                        float tu=u*48,tv=v*48; bool over=(Mathf.FloorToInt(tu)+Mathf.FloorToInt(tv))%2==0;
                        float thread=Mathf.Sin((over?Frac(tu):Frac(tv))*Mathf.PI); float noise=Fbm(u*5,v*5,5,3,seed);
                        t=(.8f+.2f*thread)*(.9f+.18f*noise); h=thread; g=.3f+.5f*thread;
                        break;
                    }
                    case "leaves":
                    {
                        Cells(u,v,9,seed,out float near,out float second,out float id); float noise=Fbm(u*18,v*18,18,2,seed);
                        bool edge=second-near<.07f; t=Mathf.Lerp(.62f,1.14f,id)*(edge?.6f:1)*(.9f+.18f*noise); h=edge?0:.5f+.5f*(1-near); g=edge?.1f:.4f+.5f*id;
                        break;
                    }
                    default:
                    {
                        float noise=Fbm(u*8,v*8,8,4,seed); t=.88f+.2f*noise; h=noise; g=.7f+.3f*noise;
                        break;
                    }
                }
                height[y*Size+x]=h; tone[y*Size+x]=t; gloss[y*Size+x]=g;
            }
            var pixels=new Color[Size*Size];
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
            {
                int i=y*Size+x;
                // Relief shading from the height field, lit from the upper left: cheap depth without a normal map.
                float slope=height[y*Size+(x+1)%Size]-height[y*Size+(x+Size-1)%Size]+height[((y+1)%Size)*Size+x]-height[((y+Size-1)%Size)*Size+x];
                float value=Mathf.Lerp(1,tone[i]*(1+Mathf.Clamp(slope,-1,1)*.35f),amount);
                // Slight warm/cool drift keeps large surfaces from reading as one flat hue.
                float drift=(Fbm(x/(float)Size*3,y/(float)Size*3,3,2,seed+17)-.5f)*.1f*amount;
                pixels[i]=new Color(Mathf.Clamp01(value*(1+drift)*.92f),Mathf.Clamp01(value*.92f),Mathf.Clamp01(value*(1-drift)*.92f),Mathf.Lerp(1,gloss[i],amount));
            }
            texture.SetPixels(pixels); texture.wrapMode=TextureWrapMode.Repeat; texture.filterMode=FilterMode.Bilinear; texture.Apply(true);
        }

        // The view behind a window: dusk sky, moon, stars, far hills and a distant tower. Colours are final, not a tone mask.
        // land=false gives open sky only, for skylights seen from below.
        static void Vista(Texture2D texture,int seed,bool land)
        {
            var zenith=new Color(.04f,.07f,.2f); var middle=new Color(.2f,.36f,.5f); var horizon=new Color(.95f,.58f,.5f);
            var far=new Color(.13f,.22f,.3f); var near=new Color(.04f,.08f,.1f); var moon=new Color(1f,.96f,.82f);
            var pixels=new Color[Size*Size];
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
            {
                float u=x/(float)Size,v=y/(float)Size;
                Color c=v>.55f?Color.Lerp(middle,zenith,Mathf.SmoothStep(0,1,(v-.55f)/.45f)):Color.Lerp(horizon,middle,Mathf.SmoothStep(0,1,(v-.25f)/.3f));
                if(!land) c=Color.Lerp(middle,zenith,Mathf.SmoothStep(0,1,Mathf.Sqrt((u-.5f)*(u-.5f)+(v-.5f)*(v-.5f))*2.2f));
                if((v>.5f || !land) && Hash(x/2,y/2,seed)>.985f) c=Color.Lerp(c,Color.white,.85f);
                float distance=Mathf.Sqrt((u-.68f)*(u-.68f)+(v-.72f)*(v-.72f));
                c=Color.Lerp(c,moon,Mathf.Clamp01(1-distance/.22f)*.45f);
                if(distance<.085f) c=moon;
                if(!land) { c.a=1; pixels[y*Size+x]=c; continue; }
                float back=.37f+.04f*Mathf.Sin(u*14.4f+2)+.02f*Mathf.Sin(u*31f);
                if(v<back) c=far;
                // Tower with a pointed roof on the far ridge.
                float tower=Mathf.Abs(u-.27f);
                if((tower<.035f && v<.5f) || (v>=.5f && v<.6f && tower<.05f*(1-(v-.5f)/.1f))) c=far*.8f;
                if(tower<.008f && v>.43f && v<.46f) c=new Color(1f,.8f,.45f);
                float front=.26f+.06f*Mathf.Sin(u*9.4f+1)+.03f*Mathf.Sin(u*25f+4);
                if(v<front) c=near;
                c.a=1; pixels[y*Size+x]=c;
            }
            texture.SetPixels(pixels); texture.wrapMode=TextureWrapMode.Clamp; texture.filterMode=FilterMode.Bilinear; texture.Apply(true);
        }

        static float Frac(float value) => value-Mathf.Floor(value);

        static float Hash(int x,int y,int seed)
        {
            unchecked
            {
                uint h=(uint)(x*374761393)+(uint)(y*668265263)+(uint)(seed*2246822519u);
                h=(h^(h>>13))*1274126177u; h^=h>>16;
                return (h&0xffffff)/16777215f;
            }
        }

        // Value noise that repeats every `period` cells, so the tile is seamless.
        static float Noise(float x,float y,int period,int seed)
        {
            int x0=Mathf.FloorToInt(x),y0=Mathf.FloorToInt(y); float fx=x-x0,fy=y-y0;
            fx=fx*fx*(3-2*fx); fy=fy*fy*(3-2*fy);
            int xa=Mod(x0,period),xb=Mod(x0+1,period),ya=Mod(y0,period),yb=Mod(y0+1,period);
            return Mathf.Lerp(Mathf.Lerp(Hash(xa,ya,seed),Hash(xb,ya,seed),fx),Mathf.Lerp(Hash(xa,yb,seed),Hash(xb,yb,seed),fx),fy);
        }

        static float Fbm(float x,float y,int period,int octaves,int seed)
        {
            float sum=0,weight=.5f,total=0;
            for(int i=0;i<octaves;i++)
            {
                sum+=Noise(x,y,Mathf.Max(1,period),seed+i*31)*weight; total+=weight;
                x*=2; y*=2; period*=2; weight*=.5f;
            }
            return sum/total;
        }

        // Tileable cellular noise: distance to the nearest and second nearest feature point, and the nearest cell's id.
        static void Cells(float u,float v,int cells,int seed,out float nearest,out float second,out float id)
        {
            float x=u*cells,y=v*cells; int cx=Mathf.FloorToInt(x),cy=Mathf.FloorToInt(y);
            nearest=second=9; id=0;
            for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
            {
                int gx=cx+dx,gy=cy+dy,wx=Mod(gx,cells),wy=Mod(gy,cells);
                float px=gx+Hash(wx,wy,seed+101),py=gy+Hash(wx,wy,seed+202);
                float d=Mathf.Sqrt((px-x)*(px-x)+(py-y)*(py-y));
                if(d<nearest) { second=nearest; nearest=d; id=Hash(wx,wy,seed+303); }
                else if(d<second) second=d;
            }
        }

        static int Mod(int value,int period) => ((value%period)+period)%period;
    }
}
