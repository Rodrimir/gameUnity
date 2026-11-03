using GameYunit.Combat;
using UnityEngine;

namespace GameYunit.Characters
{
    /// <summary>Arma baixa em polígonos, presa à mão animada e visível localmente em cada peer.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterWeaponVisual : MonoBehaviour
    {
        private static Material _weaponMaterial;
        private AutoAttackSystem _attack;
        private Transform _weaponRoot;
        private int _lastWeapon = -1;

        public void Initialize(AutoAttackSystem attack) => _attack = attack;

        private void LateUpdate()
        {
            if (_attack == null || !_attack.IsSpawned || _attack.CurrentWeapon == null) return;
            int index = _attack.CurrentWeaponIndex;
            if (index == _lastWeapon && _weaponRoot != null) return;
            _lastWeapon = index;
            Rebuild(index);
        }

        private void Rebuild(int index)
        {
            if (_weaponRoot != null) Destroy(_weaponRoot.gameObject);
            Animator animator = GetComponentInChildren<Animator>();
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null && animator != null) hand = FindHand(animator.transform);
            if (hand == null) return;
            _weaponRoot = new GameObject("Weapon_HandVisual").transform;
            _weaponRoot.SetParent(hand, false);
            _weaponRoot.localPosition = new Vector3(0.025f, -0.025f, 0.07f);
            _weaponRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Color tint = index == 0 ? new Color(0.2f, 0.9f, 1f) : index == 1 ? new Color(1f, 0.72f, 0.24f) : new Color(0.72f, 0.85f, 1f);
            if (_weaponMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _weaponMaterial = new Material(shader);
            }
            _weaponMaterial.color = tint;
            AddPart("Grip", PrimitiveType.Cube, Vector3.zero, new Vector3(.09f,.22f,.09f), Color.black);
            AddPart("Body", PrimitiveType.Cube, new Vector3(0,.1f,.04f), index == 0 ? new Vector3(.12f,.24f,.12f) : new Vector3(.16f,.32f,.13f), tint);
            AddPart("Muzzle", PrimitiveType.Cylinder, new Vector3(0,.3f,.04f), new Vector3(.055f,.22f,.055f), Color.white);
            if (index == 0) AddPart("ArcaneCore", PrimitiveType.Sphere, new Vector3(0,.17f,.11f), Vector3.one * .11f, Color.white);
            if (index == 1) AddPart("SideBarrel", PrimitiveType.Cylinder, new Vector3(.08f,.28f,.04f), new Vector3(.035f,.16f,.035f), Color.white);
            if (index == 2) AddPart("PiercingRail", PrimitiveType.Cube, new Vector3(0,.18f,-.06f), new Vector3(.07f,.26f,.08f), Color.white);
        }

        private void AddPart(string partName, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Color tint)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(_weaponRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = _weaponMaterial;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(block);
        }

        private static Transform FindHand(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.Contains("hand_r") || child.name.Contains("R Hand")) return child;
            return null;
        }
    }
}
