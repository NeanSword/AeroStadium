using System;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public sealed partial class AnimatedSurfaceContact
    {
        /// <summary>At least one visible surface supplied current readable triangles in the last sphere query.</summary>
        public bool SphereGeometryAvailable { get; private set; }
        /// <summary>The last sphere query was invalid or could not inspect every visible discovered surface.</summary>
        public bool SphereQueryFailed { get; private set; }

        struct SphereContact
        {
            public double at, separationSquared;
            public float radius;
            public Vector3 center, point, normal;
            public bool found;
        }

        /// <summary>
        /// First contact of a continuously translated sphere with the current animated triangles.
        /// The point belongs to the triangle; the normal points from it toward the sphere center.
        /// Animation is frozen at the current frame snapshot, not swept between animation frames.
        /// A sphere already overlapping the surface returns its starting center and nearest point.
        /// </summary>
        public bool TrySweptSphere(Vector3 from,Vector3 to,float radius,
            out Vector3 center,out Vector3 point,out Vector3 normal)
        {
            center=point=to;normal=Vector3.zero;LastSurfaceError=-1;LastMeshName=null;
            SphereGeometryAvailable=SphereQueryFailed=false;
            if(!Ready)
            {
                SphereQueryFailed=true;
                if(string.IsNullOrEmpty(LastFailure))LastFailure="No animated triangle surface is available.";
                return false;
            }
            if(!Finite(from)||!Finite(to)||!Finite(radius)||radius<0)
            {SphereQueryFailed=true;LastFailure="Sphere query has a nonfinite position or invalid radius.";return false;}
            Vector3 delta=to-from;
            if(!Finite(delta))
            {SphereQueryFailed=true;LastFailure="Sphere query displacement is nonfinite.";return false;}
            Vector3 expansion=Vector3.one*radius;
            Vector3 sweepLo=Vector3.Min(from,to)-expansion,sweepHi=Vector3.Max(from,to)+expansion;
            var hit=new SphereContact{at=1,radius=radius,separationSquared=double.PositiveInfinity};
            foreach(var item in surfaces)
            {
                if(!Visible(item.renderer))continue;
                if(item.failed)
                {SphereQueryFailed=true;LastFailure="Animated triangle surface remains unusable: "+item.label;continue;}
                if(!Refresh(item))
                {
                    SphereQueryFailed=true;
                    if(!item.failed)LastFailure="Visible mesh has no readable triangle geometry: "+item.label;
                    continue;
                }
                SphereGeometryAvailable=true;
                var positions=item.world;var indices=item.indices;
                for(int triangle=0;triangle+2<indices.Count;triangle+=3)
                {
                    int ia=indices[triangle],ib=indices[triangle+1],ic=indices[triangle+2];
                    if((uint)ia>=(uint)positions.Length||(uint)ib>=(uint)positions.Length||(uint)ic>=(uint)positions.Length)
                    {SphereQueryFailed=true;LastFailure="Animated triangle has an invalid vertex index: "+item.label;continue;}
                    Vector3 a=positions[ia],b=positions[ib],c=positions[ic];
                    Vector3 lo=Vector3.Min(a,Vector3.Min(b,c)),hi=Vector3.Max(a,Vector3.Max(b,c));
                    // These bounds only reject triangles. Every accepted contact uses their geometry.
                    if(lo.x>sweepHi.x||hi.x<sweepLo.x||lo.y>sweepHi.y||hi.y<sweepLo.y||lo.z>sweepHi.z||hi.z<sweepLo.z)continue;
                    TriangleTests++;
                    if(!SphereTriangle(from,delta,radius,a,b,c,ref hit))continue;
                    LastMeshName=item.label;
                    LastSurfaceError=(hit.point-SphereClosestPoint(hit.point,a,b,c)).magnitude;
                }
            }
            if(!SphereGeometryAvailable&&!SphereQueryFailed)
            {SphereQueryFailed=true;LastFailure="No visible readable animated triangle surface is available.";}
            if(SphereQueryFailed){LastSurfaceError=-1;LastMeshName=null;return false;}
            if(!hit.found)return false;
            center=hit.center;point=hit.point;normal=hit.normal;return true;
        }

        static bool SphereTriangle(Vector3 from,Vector3 delta,float radius,
            Vector3 a,Vector3 b,Vector3 c,ref SphereContact hit)
        {
            Vector3 face=Vector3.Cross(b-a,c-a);
            double areaSquared=SphereDot(face,face);
            if(areaSquared>0)face/=(float)Math.Sqrt(areaSquared);
            else face=Vector3.zero;
            Vector3 closest=SphereClosestPoint(from,a,b,c),offset=from-closest;
            double radiusSquared=(double)radius*radius;
            bool changed=false;
            if(SphereDot(offset,offset)<=radiusSquared)
                changed=SphereAccept(0,from,delta,closest,face,ref hit);
            if(hit.at==0||SphereDot(delta,delta)==0)return changed;

            // Two offset face planes, three finite edge cylinders, and three endpoint spheres
            // form the exact rounded triangle. There are no temporal samples or proxy contacts.
            double speed=SphereDot(delta,face),height=SphereDot(from-a,face);
            if(speed!=0&&areaSquared>0)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    double at=(side*(double)radius-height)/speed;
                    if(at<0||at>hit.at)continue;
                    Vector3 projected=from+delta*(float)at-face*(side*radius);
                    if(SphereInside(projected,a,b,c,out Vector3 onFace))
                        changed|=SphereAccept(at,from,delta,onFace,face,ref hit);
                }
            }
            changed|=SphereEdge(from,delta,radius,a,b,face,ref hit);
            changed|=SphereEdge(from,delta,radius,b,c,face,ref hit);
            changed|=SphereEdge(from,delta,radius,c,a,face,ref hit);
            changed|=SphereVertex(from,delta,radius,a,face,ref hit);
            changed|=SphereVertex(from,delta,radius,b,face,ref hit);
            changed|=SphereVertex(from,delta,radius,c,face,ref hit);
            return changed;
        }

        static bool SphereEdge(Vector3 from,Vector3 delta,float radius,Vector3 a,Vector3 b,
            Vector3 face,ref SphereContact hit)
        {
            Vector3 edge=b-a,offset=from-a;
            double edgeSquared=SphereDot(edge,edge);
            if(edgeSquared==0)return false;
            // Cross products evaluated in double precision avoid cancellation for near-parallel motion.
            SphereCrossDot(delta,edge,offset,edge,out double motionSquared,out double mixed,out double offsetSquared);
            if(!SphereRoots(motionSquared/edgeSquared,mixed/edgeSquared,
                offsetSquared/edgeSquared-(double)radius*radius,out double first,out double second))return false;
            bool changed=SphereEdgeRoot(first,from,delta,a,edge,edgeSquared,face,ref hit);
            changed|=SphereEdgeRoot(second,from,delta,a,edge,edgeSquared,face,ref hit);
            return changed;
        }

        static bool SphereEdgeRoot(double at,Vector3 from,Vector3 delta,Vector3 a,Vector3 edge,
            double edgeSquared,Vector3 face,ref SphereContact hit)
        {
            if(at<0||at>hit.at)return false;
            double along=(SphereDot(from-a,edge)+at*SphereDot(delta,edge))/edgeSquared;
            if(along<0||along>1)return false;
            return SphereAccept(at,from,delta,a+edge*(float)along,face,ref hit);
        }

        static bool SphereVertex(Vector3 from,Vector3 delta,float radius,Vector3 vertex,
            Vector3 face,ref SphereContact hit)
        {
            Vector3 offset=from-vertex;
            if(!SphereRoots(SphereDot(delta,delta),SphereDot(offset,delta),
                SphereDot(offset,offset)-(double)radius*radius,out double first,out double second))return false;
            bool changed=SphereAccept(first,from,delta,vertex,face,ref hit);
            changed|=SphereAccept(second,from,delta,vertex,face,ref hit);
            return changed;
        }

        // a*t*t + 2*b*t + c = 0, using a stable quadratic formula including exact tangencies.
        static bool SphereRoots(double a,double b,double c,out double first,out double second)
        {
            first=second=0;
            if(a<=0)return false;
            double discriminant=b*b-a*c;
            if(discriminant<0)return false;
            double root=Math.Sqrt(discriminant),q=-b-(b>=0?root:-root);
            if(q==0){first=second=-b/a;return true;}
            first=q/a;second=c/q;
            if(first>second){double swap=first;first=second;second=swap;}
            return true;
        }

        static bool SphereAccept(double at,Vector3 from,Vector3 delta,Vector3 point,
            Vector3 face,ref SphereContact hit)
        {
            if(double.IsNaN(at)||double.IsInfinity(at)||at<0||at>hit.at)return false;
            Vector3 center=from+delta*(float)at,outward=center-point;
            double separationSquared=SphereDot(outward,outward);
            if(hit.found&&at==hit.at&&separationSquared>=hit.separationSquared)return false;
            double length=Math.Sqrt(separationSquared);
            if(length>0&&hit.radius>0)outward/=(float)length;
            else if(face.sqrMagnitude>0)outward=SphereDot(face,delta)>0?-face:face;
            else outward=delta.sqrMagnitude>0?-delta.normalized:Vector3.up;
            hit.at=at;hit.center=center;hit.point=point;hit.normal=outward;
            hit.separationSquared=separationSquared;hit.found=true;return true;
        }

        // Barycentric reconstruction keeps the reported point on the actual triangle.
        static bool SphereInside(Vector3 point,Vector3 a,Vector3 b,Vector3 c,out Vector3 closest)
        {
            closest=a;
            Vector3 ab=b-a,ac=c-a,ap=point-a;
            SphereCrossDot(ap,ac,ab,ac,out _,out double numeratorU,out double denominator);
            if(denominator<=0)return false;
            SphereCrossDot(ab,ap,ab,ac,out _,out double numeratorV,out _);
            double u=numeratorU/denominator,v=numeratorV/denominator;
            if(u<0||v<0||u+v>1)return false;
            closest=a+ab*(float)u+ac*(float)v;return true;
        }

        static Vector3 SphereClosestPoint(Vector3 point,Vector3 a,Vector3 b,Vector3 c)
        {
            // A valid perpendicular projection is closest; edges also handle degenerate triangles.
            if(SphereInside(point,a,b,c,out Vector3 projected))return projected;
            Vector3 closest=SphereClosestEdge(point,a,b),other=SphereClosestEdge(point,b,c);
            if(SphereDot(point-other,point-other)<SphereDot(point-closest,point-closest))closest=other;
            other=SphereClosestEdge(point,c,a);
            if(SphereDot(point-other,point-other)<SphereDot(point-closest,point-closest))closest=other;
            return closest;
        }

        static Vector3 SphereClosestEdge(Vector3 point,Vector3 a,Vector3 b)
        {
            Vector3 edge=b-a;double lengthSquared=SphereDot(edge,edge);
            if(lengthSquared==0)return a;
            double along=SphereDot(point-a,edge)/lengthSquared;
            return a+edge*(float)Math.Max(0,Math.Min(1,along));
        }

        static double SphereDot(Vector3 a,Vector3 b)=>(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z;

        static void SphereCrossDot(Vector3 a,Vector3 b,Vector3 c,Vector3 d,
            out double firstSquared,out double mixed,out double secondSquared)
        {
            double x=(double)a.y*b.z-(double)a.z*b.y,y=(double)a.z*b.x-(double)a.x*b.z,z=(double)a.x*b.y-(double)a.y*b.x;
            double u=(double)c.y*d.z-(double)c.z*d.y,v=(double)c.z*d.x-(double)c.x*d.z,w=(double)c.x*d.y-(double)c.y*d.x;
            firstSquared=x*x+y*y+z*z;mixed=x*u+y*v+z*w;secondSquared=u*u+v*v+w*w;
        }
    }
}
