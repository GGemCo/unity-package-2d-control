using System;
using GGemCo2DControl;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using UnityEditor;

namespace GGemCo2DControlEditor
{
    /// <summary>
    /// 플레이어 가드 설정의 성공 사운드를 게임 전역 사운드 사용 매니페스트에 등록합니다.
    /// </summary>
    public sealed class PlayerGuardSoundUsageContributor :
        ISoundUsageManifestContributor,
        ISoundUsageManifestSourceContributor
    {
        /// <inheritdoc />
        public int Order => 50;

        /// <inheritdoc />
        public string DisplayName => "Control 패키지 플레이어 가드 사운드 분석";

        /// <inheritdoc />
        public void Collect(SoundUsageManifestBuildContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            string settingsPath = ConfigAddressableSettingControl.PlayerGuardSettings.Path;
            GGemCoPlayerGuardSettings settings =
                AssetDatabase.LoadAssetAtPath<GGemCoPlayerGuardSettings>(settingsPath);
            if (settings == null)
            {
                context.AddWarning(
                    $"플레이어 가드 설정을 찾지 못해 성공 사운드 분석을 건너뜁니다. path={settingsPath}");
                return;
            }

            AddGuardSoundUsage(
                context,
                settings.guardSuccessSoundUid,
                settingsPath,
                nameof(settings.guardSuccessSoundUid),
                "일반 가드 성공 사운드");
            AddGuardSoundUsage(
                context,
                settings.justGuardSuccessSoundUid,
                settingsPath,
                nameof(settings.justGuardSuccessSoundUid),
                "저스트 가드 성공 사운드");
        }

        /// <inheritdoc />
        public void CollectSourcePaths(SoundUsageManifestSourceContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.AddPath(ConfigAddressableSettingControl.PlayerGuardSettings.Path);
        }

        /// <summary>
        /// 가드 설정의 단일 대표 사운드 UID를 검증하고 전역 사용처로 등록합니다.
        /// 가드 피드백은 단발 효과음이어야 하므로 SFX가 아닌 대표 사운드는 경고 후 제외합니다.
        /// </summary>
        /// <param name="context">대표 사운드 조회 및 전역 사용처 등록 컨텍스트입니다.</param>
        /// <param name="soundUid">검증할 대표 사운드 UID입니다.</param>
        /// <param name="settingsPath">원본 플레이어 가드 설정 에셋 경로입니다.</param>
        /// <param name="fieldName">원본 설정 필드 이름입니다.</param>
        /// <param name="memo">매니페스트에서 표시할 사용처 설명입니다.</param>
        private static void AddGuardSoundUsage(
            SoundUsageManifestBuildContext context,
            int soundUid,
            string settingsPath,
            string fieldName,
            string memo)
        {
            if (soundUid <= 0)
                return;

            if (!context.TryGetSound(soundUid, out StruckTableSound sound))
            {
                context.AddWarning(
                    $"플레이어 가드 설정이 sound 테이블에 없는 UID를 참조합니다. field={fieldName}, soundUid={soundUid}, path={settingsPath}");
                return;
            }

            if (sound.Type != SoundConstants.Type.Sfx)
            {
                context.AddWarning(
                    $"플레이어 가드 성공 사운드는 Sfx 타입이어야 합니다. field={fieldName}, soundUid={soundUid}, type={sound.Type}, path={settingsPath}");
                return;
            }

            context.AddGlobalSoundUsage(
                soundUid,
                SoundUsageManifestSourceType.PackageSettings,
                sourceUid: 0,
                sourcePath: $"{settingsPath}#{fieldName}",
                memo: memo);
        }
    }
}
