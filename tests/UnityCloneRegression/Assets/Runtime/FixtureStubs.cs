[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]
namespace IncreaseHardMod
{
    internal static class Plugin { internal static readonly FixtureLog Log=new FixtureLog(); }
    internal sealed class FixtureLog { internal void LogError(string text){UnityEngine.Debug.LogError(text);} internal void LogInfo(string text){UnityEngine.Debug.Log(text);} }
}

// Game/network dependencies for exercising the real WaterWatch lifecycle and renderer.
// These do not validate multiplayer transport, input routing or native audio.
namespace Photon.Pun
{
    public enum RpcTarget { MasterClient, Others, All }
    public class PunRPC:System.Attribute {}
    public class FixturePlayer { public int ActorNumber; }
    public struct PhotonMessageInfo { public FixturePlayer Sender; }
    public class PhotonView:UnityEngine.MonoBehaviour
    {
        public int ViewID,OwnerActorNr;
        public void RPC(string method,RpcTarget target,params object[] args) {}
    }
    public static class PhotonNetwork { public static double Time=>UnityEngine.Time.timeAsDouble; }
}
public class PhysGrabObject:UnityEngine.MonoBehaviour
{
    public bool grabbedLocal,dead;
    public UnityEngine.Vector3 midPointOffset,boundingBox;
    public System.Collections.Generic.List<UnityEngine.Transform> colliders=new System.Collections.Generic.List<UnityEngine.Transform>();
    public System.Collections.Generic.List<FixtureGrabber> playerGrabbing=new System.Collections.Generic.List<FixtureGrabber>();
}
public class FixtureGrabber
{
    public UnityEngine.Transform physGrabPoint;
    public UnityEngine.Vector3 physGrabPointPullerPosition;
    public UnityEngine.Collider grabbedPhysGrabObjectCollider;
    public Photon.Pun.PhotonView photonView;
    public void ReleaseObject(int id){}
}
public class PhysGrabObjectCollider:UnityEngine.MonoBehaviour { public int colliderID; }
public class ValuableObject:UnityEngine.MonoBehaviour { public bool dollarValueSet=true;public float dollarValueCurrent=9500; }
public class PhysGrabObjectImpactDetector:UnityEngine.MonoBehaviour
{
    public UnityEngine.Events.UnityEvent onAllBreaks=new UnityEngine.Events.UnityEvent();
    public int breakLevelLight,paid;
    public void Break(int debit,UnityEngine.Vector3 position,int level,bool force){paid+=debit;GetComponent<ValuableObject>().dollarValueCurrent-=debit;onAllBreaks.Invoke();}
}
public class HurtCollider:UnityEngine.MonoBehaviour
{
    public enum BreakImpact { None,Light }
    public bool physLogic,physDestroy;
    public BreakImpact physImpact;
}
public class PlayerAvatar:UnityEngine.MonoBehaviour
{
    public static PlayerAvatar instance;
    public object physGrabber;
    public Photon.Pun.PhotonView photonView;
}
public static class GameManager { public static bool Multiplayer()=>false; }
public static class SemiFunc
{
    public static System.Collections.Generic.List<PlayerAvatar> PlayerGetList()=>null;
    public static bool IsMasterClientOrSingleplayer()=>true;
    public static bool NoTextInputsActive()=>false;
    public static bool MasterOnlyRPC(Photon.Pun.PhotonMessageInfo info)=>true;
    public static PlayerAvatar PlayerAvatarGetFromPhotonID(int id)=>null;
}
namespace HarmonyLib
{
    public class HarmonyPatch:System.Attribute {}
    public static class AccessTools
    {
        public static System.Reflection.MethodInfo DeclaredMethod(System.Type type,string name)=>null;
    }
}
public class GumballValuable:UnityEngine.MonoBehaviour
{
    public UnityEngine.GameObject hypnosisLine;
    public UnityEngine.Transform HypnosisLinesParenTransform;
}
public class EyeLines:UnityEngine.MonoBehaviour
{
    public PlayerAvatar target;
    public void InitializeLine(PlayerAvatar player){target=player;}
    public void SetIsActive(bool state){}
}
namespace IncreaseHardMod.Valuables
{
    internal class FixtureConfig<T> { public T Value; }
    internal static class InteractiveValuables
    {
        internal static FixtureConfig<float> FlowSeconds;
        internal static FixtureConfig<UnityEngine.KeyCode> FlipKey;
    }
    internal class ValuableFoley:UnityEngine.MonoBehaviour
    {
        internal static readonly int Drip=0,Flip=1,Glass=2,Tube=9;
        internal void Emit(int sound,float volume) {}
    }
    internal class ValuableContact:UnityEngine.MonoBehaviour { internal ValuableFoley owner; }
}
namespace IncreaseHardMod.Valuables
{
    // Fixture metadata only. Hierarchy rebinding and donor removal use copied production code.
    [System.Serializable] internal sealed class ValuableArtModel { public ValuableArtPart[] parts;public FixtureArtMaterial[] materials;public UnityEngine.Vector3 beckonPivot;public ValuableArtPhysics physics; }
    [System.Serializable] internal sealed class ValuableArtPhysics { public UnityEngine.Vector3 centerOfMass,motionMin,motionMax;public ValuableArtCollider[] colliders; }
    [System.Serializable] internal sealed class ValuableArtCollider { public string name;public bool moving;public UnityEngine.Vector3[] vertices;public int[] triangles; }
    [System.Serializable] internal sealed class FixtureArtMaterial { public string name;public UnityEngine.Color color;public float emission; }
    [System.Serializable] internal sealed class ValuableArtPart
    {
        public string group;public float phase;public int material;
        public UnityEngine.Vector3[] vertices,normals;public int[] triangles;
        public FixturePose rest;
    }
    [System.Serializable] internal sealed class FixturePose { public UnityEngine.Vector3 position; }
    internal static class ValuableVisuals
    {
        internal static readonly System.Collections.Generic.Dictionary<string,ValuableArtModel> Overrides=new System.Collections.Generic.Dictionary<string,ValuableArtModel>();
        internal static ValuableArtModel Load(string key)
        {
            if(Overrides.TryGetValue(key,out var model))return model;
            if(key!="water_timer")throw new System.Exception("Unexpected fixture asset: "+key);
            return new ValuableArtModel{parts=new[]{new ValuableArtPart{group="case"},new ValuableArtPart{group="drop",phase=.25f}}};
        }
    }
}
