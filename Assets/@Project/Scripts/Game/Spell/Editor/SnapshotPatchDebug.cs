#if UNITY_EDITOR
using Cysharp.Text;
using SpecData;
using UnityEditor;
using UnityEngine;

namespace Game.Spell.EditorDebug
{
    // Spell.md §3 canonical example — 강화탄 + 경량탄 + 데미지 업.
    // base damage=1, bounce=5 → expected damage=3, bounce=7.
    public static class SnapshotPatchDebug
    {
        [MenuItem("Tools/ChainBall/Spell/Verify SnapshotPatch")]
        private static void Verify()
        {
            var heavy = new SpecModifier
            {
                id = "heavy", damageDelta = 2, damageMul = 1f, damageMin = 1,
                bounceDelta = -2, hitWidthMul = 1f, speedMul = 1f,
            };
            var light = new SpecModifier
            {
                id = "light", damageDelta = -1, damageMul = 1f, damageMin = 1,
                bounceDelta = 4, hitWidthMul = 1f, speedMul = 1f,
            };
            var dmgUp = new SpecModifier
            {
                id = "dmg_up", damageDelta = 1, damageMul = 1f, damageMin = 1,
                hitWidthMul = 1f, speedMul = 1f,
            };

            var patch = SnapshotPatch.Get().Apply(heavy).Apply(light).Apply(dmgUp);

            var snap = HitSnapshot.Get();
            snap.BaseDamage = 1f;
            snap.Speed = 10f;
            patch.ApplyTo(snap, hitSpec: null);

            int baseBounce = 5;
            int finalBounce = baseBounce + patch.BounceDelta;

            Debug.Log(ZString.Format(
                "[SnapshotPatch] damage = {0} (expect 3), bounce = {1} (expect 7), speed = {2} (expect 10)",
                snap.BaseDamage, finalBounce, snap.Speed));

            bool damageOk = Mathf.Approximately(snap.BaseDamage, 3f);
            bool bounceOk = finalBounce == 7;
            if (damageOk && bounceOk) Debug.Log("[SnapshotPatch] PASS");
            else Debug.LogError("[SnapshotPatch] FAIL");

            patch.Dispose();
            snap.Dispose();
        }
    }
}
#endif
