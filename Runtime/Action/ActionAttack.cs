using System.Collections;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    public class ActionAttack
    {
        private CharacterBase _characterBase;
        private CharacterBaseController _characterBaseController;
        private int _currentCombo;
        private int _countCombo;
        private Coroutine _coroutineDonAttack;
        private InputManager _inputManager;
        private GGemCoAttackComboSettings _attackComboSettings;

        public void Initialize(InputManager inputManager, CharacterBase characterBase)
        {
            _inputManager = inputManager;
            _characterBase = characterBase;
            _characterBase.AnimationCompleteAttack += OnAnimationCompleteAttack;
            _characterBase.AnimationCompleteAttackEnd += OnAnimationCompleteAttackEnd;
            _characterBase.OnStop += OnStop;
            _attackComboSettings = AddressableLoaderSettingsControl.Instance.attackComboSettings;
            _countCombo = _attackComboSettings.GetCountCombo();
        }

        public void OnDestroy()
        {
            if (!_characterBase) return;
            _characterBase.AnimationCompleteAttack -= OnAnimationCompleteAttack;
            _characterBase.AnimationCompleteAttackEnd -= OnAnimationCompleteAttackEnd;
            _characterBase.OnStop -= OnStop;
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
            if (_coroutineDonAttack == null) return;
            _inputManager.StopCoroutine(_coroutineDonAttack);
            _coroutineDonAttack = null;
        }
        private void MoveForward(string attackAnimName)
        {
            float duration =
                _characterBase.CharacterAnimationController.GetCharacterAnimationDuration(attackAnimName, false);
            float addMove = _attackComboSettings.GetMoveForwardDistance(_currentCombo);
            float speed = _attackComboSettings.GetMoveForwardSpeed(_currentCombo);
            if (_characterBase.IsFlipped())
            {
                addMove *= -1;
            }
            _characterBase.AddMoveForce(addMove, 0, duration * speed);
        }
        private IEnumerator CoroutinePlayAttackEndAnimation()
        {
            // GcLogger.Log($"PlayAttackEndAnimation wait time:{_attackComboSettings.GetWaitTime(_currentCombo)}");
            yield return new WaitForSeconds(_attackComboSettings.GetWaitTime(_currentCombo));
            _characterBase.CharacterAnimationController?.PlayAttackEndAnimation();
        }

        public void Attack(InputAction.CallbackContext ctx)
        {
            // if (ctx.started)  
            //     GcLogger.Log($"on attack Press ");
            // else if (ctx.performed)
            //     GcLogger.Log($"on attack Hold  ");
            // else if (ctx.canceled) 
            //     GcLogger.Log($"on attack Release");
            
            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusDead()) return;
            if (_countCombo <= 0) return;

            // 콤보 리스트가 1개 초과 일때만 콤보 처리 
            if (_countCombo > 1)
            {
                // 마지막 모션이면 처리하지 않기
                if (IsLastAttackCombo()) return;
            
                if (_characterBase.IsStatusAttackComboWait())
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
                if (_characterBase.IsStatusAttackComboWait()) return;
                SetAttackCombo(0);
            }
            
            _characterBase.SetStatusAttack(); // 공격 중 상태 설정
            _characterBase.directionNormalize = Vector3.zero; // 움직임 멈춤
            string attackAnimName = _attackComboSettings.GetAnimationName(_currentCombo);
            
            // 추가 데미지 affect 적용
            float duration = _characterBase.CharacterAnimationController.GetCharacterAnimationDuration(attackAnimName);
            int affectUid = _attackComboSettings.GetAffectUid(_currentCombo);
            if (affectUid > 0)
            {
                _characterBase.AddAffect(affectUid, duration);
            }

            // 공격시 앞으로 조금씩 이동하기
            MoveForward(attackAnimName);

            _characterBase.CharacterAnimationController?.PlayAttackAnimation(attackAnimName);
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
            _coroutineDonAttack = sender.StartCoroutine(CoroutinePlayAttackEndAnimation());
            
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
            _characterBase.Stop();
            
            // 처리 완료 선언 (레거시 폴백 차단)
            e.Handled = true;
        }

        private void OnStop(CharacterBase sender, EventArgsOnStop e)
        {
            // 이미 다른 상위 시스템이 처리했으면 패스
            if (e.Handled) return;
            
            ClearAttackCombo();
            
            // 처리 완료 선언 (레거시 폴백 차단)
            e.Handled = true;
        }
    }
}