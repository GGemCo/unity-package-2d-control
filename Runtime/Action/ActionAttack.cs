using System.Collections;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    public class ActionAttack : ActionBase
    {
        private int _currentCombo;
        private int _countCombo;
        private GGemCoAttackComboSettings _attackComboSettings;
        private Coroutine _coroutineDontAttack;
        private Coroutine _coroutineWaitEnd;

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

        private void StopCoroutineAttackWait()
        {
            if (_coroutineDontAttack == null) return;
            actionInputManager.StopCoroutine(_coroutineDontAttack);
            _coroutineDontAttack = null;
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
            actionCharacterBase.AddMoveForce(addMove, 0, duration * speed);
        }
        private IEnumerator CoroutinePlayAttackEndAnimation()
        {
            // GcLogger.Log($"PlayAttackEndAnimation wait time:{_attackComboSettings.GetWaitTime(_currentCombo)}");
            yield return new WaitForSeconds(_attackComboSettings.GetWaitTime(_currentCombo));
            actionCharacterBase.CharacterAnimationController?.PlayAttackEndAnimation();
        }

        public void Attack(InputAction.CallbackContext ctx)
        {
            // if (ctx.started)  
            //     GcLogger.Log($"on attack Press ");
            // else if (ctx.performed)
            //     GcLogger.Log($"on attack Hold  ");
            // else if (ctx.canceled) 
            //     GcLogger.Log($"on attack Release");
            
            if (actionCharacterBase.IsStatusAttack()) return;
            if (actionCharacterBase.IsStatusDead()) return;
            if (_countCombo <= 0) return;

            // 콤보 리스트가 1개 초과 일때만 콤보 처리 
            if (_countCombo > 1)
            {
                // 마지막 모션이면 처리하지 않기
                if (IsLastAttackCombo()) return;
            
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
                if (actionCharacterBase.IsStatusAttackComboWait()) return;
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
            sender.CharacterAnimationController.PlayAttackWaitAnimation();
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
        private void OnStop(CharacterBase sender, EventArgsOnStop e)
        {
            // 이미 다른 상위 시스템이 처리했으면 패스
            if (e.Handled) return;
            
            StopWaitEnd();
            ClearAttackCombo();
            
            // 처리 완료 선언 (레거시 폴백 차단)
            e.Handled = true;
        }
    }
}