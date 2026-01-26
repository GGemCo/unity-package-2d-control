using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 데미지를 주는 오브젝트
    /// </summary>
    public class ObjectDamageArea : DefaultMapObject
    {
        public ConfigCommon.DamageType damageType;
        public long damage;
        public int affectUid;
        private Collider2D _col;

        protected override void Awake()
        {
            base.Awake();
            _col = GetComponent<Collider2D>();
            _col.isTrigger = true;
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            CharacterHitArea hitArea = other.GetComponent<CharacterHitArea>();
            if (!hitArea) return;
            // GcLogger.Log($"death zone. OnTriggerEnter2D. damage:{damage}");
            MetadataDamage metadataDamage = new MetadataDamage
            {
                damage = damage,
                damageType = damageType,
                affectUid = affectUid
            };
            hitArea.target.TakeDamage(metadataDamage);
        }
    }
}