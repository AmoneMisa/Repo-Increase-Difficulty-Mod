"""Layered hair fitted around the native eye opening; reusable by costume generators."""
import math
import numpy as np
from mathutils import Vector
import cosmetic_kit as K
import costume_parts as P

def hair(material, length=.24, wave=.012):
    parts, point, _ = P.hood('Hair cap',material,material,P.HEAD_C,Vector((.291,.418,.291)),
        lambda a: -8+72*P.front(a)**1.3,peak=0,brim=.004)
    for k in range(23):
        a=math.radians(64+232*k/22)
        top=point(a,47,.006)
        r=.38 if length>.4 else .29
        end=Vector((r*math.sin(a),-length+.035*math.sin(k*2.3),P.HEAD_C.z+r*math.cos(a)))
        controls=[top,top+Vector((.085*math.sin(a),-.12,.085*math.cos(a))),end+Vector((.028*math.sin(k),.08,.02)),end]
        tangent=Vector((math.cos(a),0,-math.sin(a)));normal=Vector((math.sin(a),0,math.cos(a)))
        def lock(t,s):
            c=K.bez(controls,t);phi=math.tau*s
            c+=tangent*(wave*math.sin(t*11+k)*math.sin(math.pi*t))
            width=.030*(1-.94*t**3)
            return c+tangent*(width*math.cos(phi))+normal*(.011*math.sin(phi))
        parts.append(K.surface('Layered tapered hair lock',22,10,lock,material,wrap=True))
    return parts
