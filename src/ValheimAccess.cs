using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // Vanilla 1.0 hides m_nview / m_lastHit and some network functions.
    // Cache component lookups and FieldInfo; never patch game assemblies in-place.
    internal static class ValheimAccess
    {
        private sealed class Holder { internal ZNetView Value; }
        private static readonly ConditionalWeakTable<Component, Holder> Views = new ConditionalWeakTable<Component, Holder>();
        private static readonly FieldInfo LastHitField = AccessTools.Field(typeof(Character), "m_lastHit");
        private static readonly MethodInfo RpcServerPeer = AccessTools.Method(typeof(ZRoutedRpc), "GetServerPeerID");
        private static readonly MethodInfo NetServerPeer = AccessTools.Method(typeof(ZNet), "GetServerPeerID");

        internal static ZNetView View(Component component)
        {
            if (component == null) return null;
            var holder = Views.GetValue(component, c => new Holder { Value = c.GetComponent<ZNetView>() });
            return holder.Value;
        }

        internal static HitData LastHit(Character target)
        {
            if (target == null || LastHitField == null) return null;
            return LastHitField.GetValue(target) as HitData;
        }

        internal static long ServerPeerID()
        {
            try
            {
                if (RpcServerPeer != null && ZRoutedRpc.instance != null)
                    return Convert.ToInt64(RpcServerPeer.Invoke(ZRoutedRpc.instance, null));
                if (NetServerPeer != null && ZNet.instance != null)
                    return Convert.ToInt64(NetServerPeer.Invoke(ZNet.instance, null));
            }
            catch (Exception) { }
            return 0L; // fail closed: don't award points from unverified RPC senders
        }
    }
}
