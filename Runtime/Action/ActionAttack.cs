using System.Collections;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    public class ActionAttack : ActionBase
    {
        private int _currentCombo;
        private int _countCombo;
        private GGemCoAttackComboSettings _attackComboSettings;
        private Coroutine _coroutineDontAttack;
        private Coroutine _coroutineWaitEnd;
        private float _attackWaitElapsedSeconds;

        public override void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            // ApplySettings 에서 사용해야 하기때문에 먼저 선언
            _attackComboSettings = AddressableLoaderSettingsControl.Instance.attackComboSettings;
            base.Initialize(inputManager, characterBase, characterBaseController);
            
            actionCharacterBase.AnimationCompleteAttack += OnAnimationCompleteAttack;
            actionCharacterBase.AnimationCompleteAttackEnd += OnAnimationCompleteAttackEnd;
            actionCharacterBase.OnStop += OnStop;
        }
        protected override void ApplySettings()
        {
            _countCombo = _attackComboSettings.GetCountCombo();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (!actionCharacterBase) return;
            actionCharacterBase.AnimationCompleteAttack -= OnAnimationCompleteAttack;
            actionCharacterBase.AnimationCompleteAttackEnd -= OnAnimationCompleteAttackEnd;
            actionCharacterBase.OnStop -= OnStop;
        }

        private void ClearAttackCombo()
        {
            _currentCombo = -1;
        }

        private void SetAttackCombo(int attackCombo)
        {
            _currentCombo = attackCombo;
        }
        public int GetAttackCombo()
        {
            return _currentCombo;
        }

        /// <summary>
        /// 현재 기본 공격 콤보 단계 정보를 조회합니다.
        /// </summary>
        /// <param name="state">현재 기본 공격 콤보 단계 정보입니다.</param>
        /// <returns>유효한 기본 공격 콤보 단계가 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool TryGetCurrentComboState(out AttackComboRuntimeState state)
        {
            state = default;
            if (_countCombo <= 0 || _currentCombo < 0 || _currentCombo >= _countCombo)
            {
                return false;
            }

            state = new AttackComboRuntimeState(_currentCombo, _countCombo);
            return true;
        }

        /// <summary>
        /// 현재 공격 콤보 단계에 설정된 HitStop 정책을 조회합니다.
        /// </summary>
        /// <param name="settings">현재 콤보 단계의 HitStop 설정입니다.</param>
        /// <returns>사용 가능한 HitStop 설정이 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool TryGetCurrentHitStopSettings(out AttackHitStopSettings settings)
        {
            settings = default;
            if (_attackComboSettings == null)
                return false;

            return _attackComboSettings.TryGetHitStopSettings(_currentCombo, out settings);
        }

        /// <summary>
        /// 현재 공격 콤보 단계에 설정된 카메라 Shake 정책을 조회합니다.
        /// </summary>
        /// <param name="settings">현재 콤보 단계의 카메라 Shake 설정입니다.</param>
        /// <returns>사용 가능한 카메라 Shake 설정이 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool TryGetCurrentCameraShakeSettings(out AttackCameraShakeSettings settings)
        {
            settings = AttackCameraShakeSettings.Disabled;
            if (_attackComboSettings == null)
                return false;

            return _attackComboSettings.TryGetCameraShakeSettings(_currentCombo, out settings);
        }

        /// <summary>
        /// 현재 공격 콤보 단계에 설정된 데미지 공식 정책을 조회합니다.
        /// </summary>
        /// <param name="settings">현재 콤보 단계의 데미지 공식 설정입니다.</param>
        /// <returns>사용 가능한 공식 설정이 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool TryGetCurrentDamageFormulaSettings(out AttackComboDamageFormulaSettings settings)
        {
            settings = AttackComboDamageFormulaSettings.Default;
            if (_attackComboSettings == null)
                return false;

            return _attackComboSettings.TryGetDamageFormulaSettings(_currentCombo, out settings);
        }

        private bool IsLastAttackCombo()
        {
            return _currentCombo == _countCombo - 1;
        }
        /// <summary>
        /// 다음 콤보 셋팅하기
        /// </summary>
        private void NextAttackCombo()
        {
            _currentCombo++;
        }

        /// <summary>
        /// 공격 콤보 대기 후 attack_end 애니메이션을 재생하기 위해 예약된 코루틴을 중단합니다.
        /// </summary>
        private void StopCoroutineAttackWait()
        {
            if (_coroutineDontAttack == null) return;
            actionInputManager.StopCoroutine(_coroutineDontAttack);
            _coroutineDontAttack = null;
            _attackWaitElapsedSeconds = 0f;
        }

        /// <summary>
        /// 다른 액션이 공격을 인터럽트할 때 남아 있는 공격 후속 예약과 콤보 상태를 정리합니다.
        /// </summary>
        public void CancelAttackByActionInterrupt()
        {
            StopPendingAttackRoutines();
            ClearAttackCombo();
        }

        /// <summary>
        /// 피격으로 인해 기본 공격 콤보를 중단하고, 설정에 따라 진행 중인 Crowd Control을 정리합니다.
        /// </summary>
        /// <param name="reason">피격으로 인한 액션 취소 사유입니다.</param>
        /// <remarks>
        /// Crowd Control 중단 여부는 콤보 상태와 <see cref="GGemCoAttackComboSettings"/> 정책을 먼저 확인한 뒤 결정합니다.
        /// 콤보 인덱스를 초기화하기 전에 판정하여 현재 공격 단계의 설정을 안정적으로 읽습니다.
        /// </remarks>
        public void CancelAttackByIncomingHit(IncomingHitCancelReason reason)
        {
            bool shouldStopCrowdControl = ShouldStopCrowdControlOnIncomingHit(reason);
            bool shouldStopMoveForward = ShouldStopMoveForwardOnIncomingHit(reason);

            StopPendingAttackRoutines();

            if (shouldStopMoveForward)
            {
                actionCharacterBase?.CancelMoveForce();
            }

            ClearAttackCombo();

            if (!shouldStopCrowdControl)
                return;

            GameObject source = actionCharacterBase != null ? actionCharacterBase.gameObject : null;
            if (!CharacterCrowdControlController.TryStopCrowdControlsBySource(source, CrowdControlStopReason.IncomingHit))
            {
                actionCharacterBase?.TryStopCrowdControl(CrowdControlStopReason.IncomingHit);
            }
        }

        /// <summary>
        /// 현재 피격 사유와 공격 콤보 정책을 기준으로 Crowd Control 중단 여부를 계산합니다.
        /// </summary>
        /// <param name="reason">피격으로 인한 액션 취소 사유입니다.</param>
        /// <returns>Crowd Control을 중단해야 하면 <see langword="true"/>를 반환합니다.</returns>
        private bool ShouldStopCrowdControlOnIncomingHit(IncomingHitCancelReason reason)
        {
            if (reason != IncomingHitCancelReason.Damage)
                return false;

            if (!IsPlayingBasicAttackCombo())
                return false;

            return _attackComboSettings != null &&
                   _attackComboSettings.ShouldStopCrowdControlOnIncomingHit(_currentCombo);
        }

        /// <summary>
        /// 현재 피격 사유와 공격 콤보 정책을 기준으로 공격 전방 이동 중단 여부를 계산합니다.
        /// </summary>
        /// <param name="reason">피격으로 인한 액션 취소 사유입니다.</param>
        /// <returns>공격 전방 이동을 중단해야 하면 <see langword="true"/>를 반환합니다.</returns>
        private bool ShouldStopMoveForwardOnIncomingHit(IncomingHitCancelReason reason)
        {
            if (reason != IncomingHitCancelReason.Damage)
                return false;

            if (!IsPlayingBasicAttackCombo())
                return false;

            return _attackComboSettings != null &&
                   _attackComboSettings.ShouldStopMoveForwardOnIncomingHit(_currentCombo);
        }

        /// <summary>
        /// 현재 캐릭터가 기본 공격 콤보 모션 또는 콤보 대기 상태를 유지하고 있는지 확인합니다.
        /// </summary>
        /// <returns>기본 공격 콤보 진행 중이면 <see langword="true"/>를 반환합니다.</returns>
        private bool IsPlayingBasicAttackCombo()
        {
            if (actionCharacterBase == null)
                return false;

            if (_countCombo <= 0 || _currentCombo < 0 || _currentCombo >= _countCombo)
                return false;

            return actionCharacterBase.IsStatusAttack() || actionCharacterBase.IsStatusAttackComboWait();
        }

        /// <summary>
        /// 공격 액션이 예약한 모든 코루틴을 중단합니다.
        /// </summary>
        private void StopPendingAttackRoutines()
        {
            StopCoroutineAttackWait();
            StopWaitEnd();
        }
        private void MoveForward(string attackAnimName)
        {
            float duration =
                actionCharacterBase.CharacterAnimationController.GetCharacterAnimationDuration(attackAnimName, false);
            float addMove = _attackComboSettings.GetMoveForwardDistance(_currentCombo);
            float speed = _attackComboSettings.GetMoveForwardSpeed(_currentCombo);
            if (actionCharacterBase.IsFlipped())
            {
                addMove *= -1;
            }
            actionCharacterBase.AddMoveForceWithCharacterBodyCollision(addMove, 0f, duration * speed);
        }
        private IEnumerator CoroutinePlayAttackEndAnimation()
        {
            _attackWaitElapsedSeconds = 0f;
            float waitSeconds = _attackComboSettings.GetWaitTime(_currentCombo);
            while (_attackWaitElapsedSeconds < waitSeconds)
            {
                AdvanceHitStopAwareUnscaled(ref _attackWaitElapsedSeconds);
                yield return null;
            }

            // 스킬 등 다른 액션이 기본 콤보를 점유한 경우, 오래된 예약이 새 애니메이션을 덮지 않도록 종료합니다.
            if (actionCharacterBase == null ||
                !actionCharacterBase.IsStatusAttackComboWait() ||
                _currentCombo < 0 ||
                _currentCombo >= _countCombo)
            {
                _coroutineDontAttack = null;
                _attackWaitElapsedSeconds = 0f;
                yield break;
            }

            _coroutineDontAttack = null;
            _attackWaitElapsedSeconds = 0f;
            actionCharacterBase.CharacterAnimationController?.PlayAttackEndAnimation();
        }

        /// <summary>
        /// 기본 공격 시작을 요청합니다.
        /// 기존 호출부와의 호환성을 유지하기 위해 공격 시작 결과는 반환하지 않습니다.
        /// </summary>
        public void Attack()
        {
            TryAttack();
        }

        /// <summary>
        /// 현재 캐릭터와 콤보 상태를 검증한 뒤 기본 공격을 시작합니다.
        /// </summary>
        /// <returns>공격 상태와 애니메이션이 실제로 시작되었으면 <see langword="true"/>입니다.</returns>
        internal bool TryAttack()
        {
            if (IsHitStopped()) return false;
            if (actionCharacterBase.IsStatusAttack()) return false;
            if (actionCharacterBase.IsStatusDead()) return false;
            if (_countCombo <= 0) return false;

            // 콤보 리스트가 1개 초과 일때만 콤보 처리 
            if (_countCombo > 1)
            {
                // 마지막 모션이면 처리하지 않기
                if (IsLastAttackCombo()) return false;
            
                if (actionCharacterBase.IsStatusAttackComboWait())
                {
                    StopCoroutineAttackWait();
                    NextAttackCombo();
                }
                else
                {
                    SetAttackCombo(0);
                }
            }
            else
            {
                // wait 타임이 있을때는 대기 한다.
                if (actionCharacterBase.IsStatusAttackComboWait()) return false;
                SetAttackCombo(0);
            }
            
            actionCharacterBase.SetStatusAttack(); // 공격 중 상태 설정
            actionCharacterBase.directionNormalize = Vector3.zero; // 움직임 멈춤
            string attackAnimName = _attackComboSettings.GetAnimationName(_currentCombo);
            
            // 추가 데미지 affect 적용
            int affectUid = _attackComboSettings.GetAffectUid(_currentCombo);
            if (affectUid > 0)
            {
                actionCharacterBase.AddAffect(affectUid);
            }

            // 공격시 앞으로 조금씩 이동하기
            MoveForward(attackAnimName);

            actionCharacterBase.CharacterAnimationController?.PlayAttackAnimation(attackAnimName);
            return true;
        }
        /// <summary>
        /// 공격 애니메이션 종료 되었을때,
        /// wait 애니메이션 root 시작
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAnimationCompleteAttack(CharacterBase sender, EventArgsAnimationAttack e)
        {
            // 이미 다른 상위 시스템이 처리했으면 패스
            if (e.Handled) return;
            
            // GcLogger.Log($"OnAnimationCompleteAttack");
            sender.SetStatusAttackComboWait();
            // wait 애니메이션이 없으면 바로 stop 처리
            var result = sender.CharacterAnimationController.PlayAttackWaitAnimation();
            if (!result)
            {
                actionCharacterBase.Stop();
                return;
            }
            StopCoroutineAttackWait();
            _coroutineDontAttack = actionInputManager.StartCoroutine(CoroutinePlayAttackEndAnimation());
            
            // 처리 완료 선언 (레거시 폴백 차단)
            e.Handled = true;
        }
        /// <summary>
        /// 공격 End 애니메이션 종료 되었을 대
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAnimationCompleteAttackEnd(CharacterBase sender, EventArgsAnimationAttackEnd e)
        {
            // 이미 다른 상위 시스템이 처리했으면 패스
            if (e.Handled) return;
            
            // GcLogger.Log($"OnAnimationCompleteAttackEnd");
            actionCharacterBase.Stop();
            
            // 처리 완료 선언 (레거시 폴백 차단)
            e.Handled = true;
        }

        private void StopWaitEnd()
        {
            if (_coroutineWaitEnd != null)
            {
                actionInputManager.StopCoroutine(_coroutineWaitEnd);
                _coroutineWaitEnd = null;
            }
        }
        /// <summary>
        /// 캐릭터 정지 시 공격 액션의 예약 작업을 정리합니다.
        /// </summary>
        /// <param name="sender">정지 이벤트를 발생시킨 캐릭터입니다.</param>
        /// <param name="e">정지 이벤트 처리 상태입니다.</param>
        private void OnStop(CharacterBase sender, EventArgsOnStop e)
        {
            // Stop(true)로 가드, CC, 탈진 등 다른 액션이 시작될 때도 attack_end 예약이 남지 않도록 먼저 정리합니다.
            StopPendingAttackRoutines();
            ClearAttackCombo();

            // 이미 다른 상위 시스템이 처리했으면 패스
            if (e.Handled) return;
            
            // 처리 완료 선언 (레거시 폴백 차단)
            e.Handled = true;
        }
    }
}
