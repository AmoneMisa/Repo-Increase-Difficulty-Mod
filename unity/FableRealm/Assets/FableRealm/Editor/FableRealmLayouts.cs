using System;
using UnityEngine;

namespace FableRealm.Editor
{
    public static partial class FableRealmBuilder
    {
        // Each room has an authored arrangement. Dimensions are metres; all external
        // sockets remain at y=0. Upper decks are optional routes, never mandatory jumps.
        static void BuildLayout(Transform root, Room room, string material)
        {
            int id = int.Parse(room.id);
            Rect? pit = null;
            switch(id)
            {
                case 3: pit = new Rect(-2.5f,-2.5f,5,5); break;
                case 4: pit = new Rect(-4.5f,-1.5f,9,3); break;
                case 8: pit = new Rect(-1.5f,-1.5f,3,3); break;
                case 13: pit = new Rect(-2.5f,-2.5f,5,5); break;
                case 15: pit = new Rect(-3.5f,-3.5f,7,7); break;
                case 19: pit = new Rect(-4.5f,-2.5f,9,5); break;
                case 22: pit = new Rect(1.5f,-2.5f,3,4); break;
            }
            Floor(root,material,pit);
            switch(id)
            {
                case 1:
                    Model(root,"Arch",new Vector3(0,0,4.8f),1.1f,0);
                    Shelter(root,-4.6f,-1.5f,0,"Bark");
                    Model(root,"Tree",new Vector3(5,0,4),.8f,0);
                    break;
                case 2: // Broken sightlines, offset root walls, two routes around them.
                    Screen(root,-2.8f,-1.4f,1,2.8f,5,"Bark");
                    Screen(root,2.8f,1.4f,1,3.3f,5,"Bark");
                    Shelter(root,4.8f,-4.8f,90,"Bark");
                    Model(root,"Tree",new Vector3(-4.8f,0,4.6f),1,25);
                    break;
                case 3:
                    Bridge(root,0,0,3.2f,5.4f,"Bark");
                    Shelter(root,-5,4.8f,180,"Bark");
                    Model(root,"Mushroom",new Vector3(5,0,4),1.6f,0);
                    Model(root,"Mushroom",new Vector3(-5,0,-4),1.1f,0);
                    break;
                case 4:
                    Bridge(root,-1.2f,0,3.2f,3.4f,"Bark");
                    Shelter(root,4.7f,4.7f,180,"Bark");
                    Model(root,"Tree",new Vector3(-5,0,-5),.9f,0);
                    break;
                case 5:
                    Table(root,-3.8f,1,3.8f,3.2f,1.8f,"Bark");
                    Table(root,3.8f,-2,3.8f,2.8f,1.8f,"Bark");
                    Model(root,"GiantCup",new Vector3(-3.8f,1.8f,1),.65f,0);
                    Screen(root,3.8f,3.8f,3,2.4f,.6f,"Bark");
                    break;
                case 6:
                    Gallery(root,2.6f,false,"Bark");
                    Shelves(root,-4,-2,3,"Bark");
                    Shelter(root,4.5f,4.5f,180,"Bark");
                    break;
                case 7:
                    Screen(root,-2.8f,-1.8f,4.8f,3,.55f,"Biscuit");
                    Screen(root,2.8f,2,4.8f,3,.55f,"Biscuit");
                    Shelter(root,-4.7f,4.8f,180,"Biscuit");
                    Model(root,"Arch",new Vector3(0,0,-5),.8f,0);
                    break;
                case 8:
                    Model(root,"Cottage",new Vector3(-4.5f,0,3),.8f,90);
                    Model(root,"Cottage",new Vector3(4.5f,0,-3),.8f,-90);
                    Shelter(root,-4.6f,-4.5f,0,"Biscuit");
                    break;
                case 9:
                    Gallery(root,2.6f,true,"Biscuit");
                    Table(root,-3.8f,-3,3.8f,2.4f,1.7f,"Bark");
                    Screen(root,-4,3.8f,2.6f,3,2,"Biscuit");
                    break;
                case 10:
                    for(int j=0;j<3;j++)
                    {
                        Table(root,j%2==0?-3.8f:3.8f,-4+j*4,3.6f,2,1.7f,"Biscuit");
                        Screen(root,j%2==0?-4.8f:4.8f,-4+j*4,.4f,2.5f,2.4f,"Roof");
                    }
                    Shelter(root,4.8f,4.8f,180,"Biscuit");
                    break;
                case 11:
                    Gallery(root,3,false,"Bark");
                    Screen(root,0,1,2,3.8f,1,"Gold");
                    Shelter(root,4.8f,-3.8f,90,"Biscuit");
                    break;
                case 12:
                    Shelves(root,-4.8f,2,3,"Biscuit");
                    Shelves(root,4.8f,2,3,"Biscuit");
                    Shelter(root,-4.8f,-4.5f,0,"Biscuit");
                    break;
                case 13:
                    Bridge(root,0,0,3.2f,5.4f,"Bark");
                    Gallery(root,3,true,"Moss");
                    Model(root,"Tree",new Vector3(4.8f,0,-4.8f),1.1f,0);
                    Shelter(root,4.8f,4.5f,180,"Bark");
                    break;
                case 14:
                    Table(root,0,1,7,5,2.6f,"Ivory");
                    Ramp(root,new Vector3(-5.2f,0,-6.7f),new Vector3(-5.2f,2.6f,-.5f),3.8f,"Ivory");
                    Platform(root,-5.2f,2.6f,1.5f,3.8f,4,"Ivory");
                    Model(root,"GiantCup",new Vector3(1.7f,2.6f,1.5f),.65f,0);
                    Shelter(root,4.8f,-4.8f,0,"Moss");
                    break;
                case 15:
                    Bridge(root,0,0,3.2f,7.4f,"Ivory");
                    Shelter(root,-5.2f,5,180,"Ivory");
                    Model(root,"GiantCup",new Vector3(5.1f,0,-5),.65f,120);
                    break;
                case 16:
                    Screen(root,-2.6f,-1.6f,.7f,2.7f,6,"Moss");
                    Screen(root,2.6f,1.6f,.7f,2.7f,6,"Moss");
                    Screen(root,-4.5f,3.5f,4.4f,2.7f,.7f,"Moss");
                    Shelter(root,4.8f,-4.8f,0,"Moss");
                    Model(root,"Rose",new Vector3(-5,0,-5),1.5f,0);
                    break;
                case 17:
                    Platform(root,3,1.2f,3.5f,6,5,"Bark");
                    Ramp(root,new Vector3(3,0,-3),new Vector3(3,1.2f,1),3.2f,"Bark");
                    Shelter(root,-4.7f,3,180,"Roof");
                    Model(root,"Tower",new Vector3(4,1.2f,4),.45f,0);
                    break;
                case 18:
                    Shelter(root,-4.8f,3,180,"Moss");
                    Table(root,4.5f,-3.5f,3,3,1.8f,"Bark");
                    Model(root,"Rose",new Vector3(5,0,5),1.4f,0);
                    break;
                case 19:
                    Bridge(root,1.1f,0,3.2f,5.4f,"Stone");
                    Model(root,"Arch",new Vector3(1.1f,0,3.5f),.8f,0);
                    Shelter(root,-4.8f,-4.8f,0,"Stone");
                    break;
                case 20:
                    Gallery(root,3.2f,false,"Ivory");
                    Screen(root,-2.6f,-2.5f,.5f,3,3,"Stone");
                    Screen(root,2.8f,.5f,.5f,3,3,"Stone");
                    Shelter(root,4.8f,-4.8f,90,"Stone");
                    break;
                case 21:
                    Gallery(root,3.2f,true,"Ivory");
                    Gallery(root,3.2f,false,"Ivory");
                    Table(root,0,1,3,2,1.7f,"Gold");
                    Shelter(root,0,4.8f,180,"Roof");
                    break;
                case 22:
                    Gallery(root,3.4f,false,"Stone");
                    Shelter(root,4.8f,4.8f,180,"Stone");
                    Model(root,"Tower",new Vector3(-4.8f,3.4f,4.5f),.4f,0);
                    break;
                case 23:
                    Shelves(root,-3.2f,-2.8f,4,"Bark");
                    Shelves(root,2.3f,1.8f,5,"Bark");
                    Shelter(root,-4.8f,4.8f,180,"Bark");
                    Table(root,4.5f,-4.5f,3.2f,2.2f,1.8f,"Bark");
                    break;
                case 24:
                    // Wide, flat extraction apron; side alcoves never occupy its centre.
                    Shelter(root,-4.8f,1,90,"Stone");
                    Shelter(root,4.8f,1,-90,"Stone");
                    Model(root,"Arch",new Vector3(0,0,5.5f),1,0);
                    break;
                default: throw new InvalidOperationException("No authored layout for "+room.id);
            }
        }

        static void Floor(Transform root,string material,Rect? pit)
        {
            if(!pit.HasValue) { Box(root,"Floor",new Vector3(0,-.2f,0),new Vector3(15,.4f,15),material); return; }
            Rect p=pit.Value;
            // Four slabs around the opening: no hidden full-plane collider below a painted pit.
            Slab(root,-7.5f,p.xMin,-7.5f,7.5f,material);
            Slab(root,p.xMax,7.5f,-7.5f,7.5f,material);
            Slab(root,p.xMin,p.xMax,-7.5f,p.yMin,material);
            Slab(root,p.xMin,p.xMax,p.yMax,7.5f,material);
            Box(root,"Pit bottom - gameplay fall behaviour pending",new Vector3(p.center.x,-4.2f,p.center.y),new Vector3(p.width,.4f,p.height),"Earth");
            Box(root,"Pit west",new Vector3(p.xMin-.1f,-2,p.center.y),new Vector3(.2f,4,p.height),"Stone");
            Box(root,"Pit east",new Vector3(p.xMax+.1f,-2,p.center.y),new Vector3(.2f,4,p.height),"Stone");
            Box(root,"Pit north",new Vector3(p.center.x,-2,p.yMax+.1f),new Vector3(p.width,4,.2f),"Stone");
            Box(root,"Pit south",new Vector3(p.center.x,-2,p.yMin-.1f),new Vector3(p.width,4,.2f),"Stone");
            Marker(root,"CHECK_PIT",new Vector3(p.xMin+.3f,0,p.yMin+.3f));
        }

        static void Slab(Transform root,float x0,float x1,float z0,float z1,string mat)
            => Box(root,"Floor slab",new Vector3((x0+x1)/2,-.2f,(z0+z1)/2),new Vector3(x1-x0,.4f,z1-z0),mat);

        static void Bridge(Transform root,float x,float z,float width,float length,string mat)
        {
            Box(root,"Bridge deck",new Vector3(x,-.12f,z),new Vector3(width,.24f,length),mat);
            for(int side=-1;side<=1;side+=2)
            {
                Box(root,"Bridge guard",new Vector3(x+side*(width/2+.1f),.75f,z),new Vector3(.15f,.15f,length),"Gold");
                for(float t=-length/2+.3f;t<length/2;t+=1.4f)
                    Box(root,"Bridge post",new Vector3(x+side*(width/2+.1f),.4f,z+t),new Vector3(.12f,.8f,.12f),mat);
            }
            Marker(root,"CHECK_BRIDGE",new Vector3(x,0,z));
        }

        static void Gallery(Transform root,float height,bool right,string mat)
        {
            float x=right?5.2f:-5.2f;
            Platform(root,x,height,3,3.8f,7,mat);
            Ramp(root,new Vector3(x,0,-6.7f),new Vector3(x,height,-.5f),3.8f,mat);
            // Inner gallery edge only; access is from the open south end of the ramp.
            Box(root,"Balcony parapet",new Vector3(x+(right?-1:1)*1.82f,height+.5f,3),new Vector3(.16f,1,6.8f),"Gold");
        }

        static void Platform(Transform root,float x,float y,float z,float width,float length,string mat)
        {
            Box(root,"Upper deck",new Vector3(x,y-.12f,z),new Vector3(width,.24f,length),mat);
            for(int a=-1;a<=1;a+=2)
                for(int b=-1;b<=1;b+=2)
                    Box(root,"Deck support",new Vector3(x+a*(width/2-.2f),y/2,z+b*Mathf.Max(.3f,length/2-1.1f)),new Vector3(.3f,y,.3f),mat);
            Marker(root,"CHECK_UPPER",new Vector3(x,y,z));
        }

        static void Ramp(Transform root,Vector3 from,Vector3 to,float width,string mat)
        {
            var delta=to-from;
            float run=new Vector2(delta.x,delta.z).magnitude;
            float slope=Mathf.Atan2(delta.y,run)*Mathf.Rad2Deg;
            var group=new GameObject("Ramp - optional upper route").transform;
            group.SetParent(root,false);
            group.localPosition=(from+to)/2-new Vector3(0,.1f/Mathf.Cos(slope*Mathf.Deg2Rad),0);
            group.localRotation=Quaternion.Euler(0,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,0)*Quaternion.Euler(-slope,0,0);
            Box(group,"Ramp surface",Vector3.zero,new Vector3(width,.2f,delta.magnitude),mat);
            Marker(root,"CHECK_RAMP",(from+to)/2);
        }

        static void Screen(Transform root,float x,float z,float width,float height,float depth,string mat)
            => Box(root,"Sightline screen",new Vector3(x,height/2,z),new Vector3(width,height,depth),mat);

        static void Shelter(Transform root,float x,float z,float yaw,string mat)
        {
            var group=new GameObject("Hide alcove - open entrance, opaque back and roof").transform;
            group.SetParent(root,false); group.localPosition=new Vector3(x,0,z); group.localRotation=Quaternion.Euler(0,yaw,0);
            Box(group,"Hide back",new Vector3(0,1.2f,1),new Vector3(2.8f,2.4f,.25f),mat);
            for(int side=-1;side<=1;side+=2)
                Box(group,"Hide side",new Vector3(side*1.3f,1.2f,0),new Vector3(.2f,2.4f,2.2f),mat);
            Box(group,"Hide roof",new Vector3(0,2.45f,0),new Vector3(2.8f,.2f,2.2f),mat);
            Marker(group,"CHECK_HIDE",new Vector3(0,1,0));
        }

        static void Table(Transform root,float x,float z,float width,float depth,float top,string mat)
        {
            Box(root,"Table canopy",new Vector3(x,top-.12f,z),new Vector3(width,.24f,depth),mat);
            for(int a=-1;a<=1;a+=2)
                for(int b=-1;b<=1;b+=2)
                    Box(root,"Table leg",new Vector3(x+a*(width/2-.2f),top/2,z+b*(depth/2-.2f)),new Vector3(.25f,top,.25f),mat);
            Marker(root,"CHECK_UNDER_TABLE",new Vector3(x,1,z));
        }

        static void Shelves(Transform root,float x,float z,float length,string mat)
        {
            Screen(root,x,z,.5f,2.8f,length,mat);
            for(int i=0;i<4;i++)
                Box(root,"Bookshelf ledge",new Vector3(x,.35f+i*.7f,z),new Vector3(1.1f,.1f,length),mat);
        }

        static void Marker(Transform parent,string name,Vector3 position)
        {
            var marker=new GameObject(name).transform;
            marker.SetParent(parent,false); marker.localPosition=position;
        }
    }
}
