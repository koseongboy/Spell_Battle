using DA_Assets.Extensions;
using DA_Assets.DAI;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    [Serializable]
    public class SpriteGenerator : FcuBase
    {
        public async Task GenerateSprites(List<FObject> fobjects, CancellationToken token)
        {
            await GenerateSprites(fobjects, null, token);
        }

        internal async Task GenerateSprites(List<FObject> fobjects, SpriteIdentityCache cache, CancellationToken token)
        {
            IEnumerable<FObject> source = cache?.UniqueRepresentatives ?? fobjects;
            List<FObject> generative = source.Where(x => x.Data.NeedGenerate).ToList();

            if (generative.IsEmpty())
                return;

            int generatedCount = 0;
            int loggedCount = -1;
            int totalCount = generative.Count;
            FObject fobject;

            Debug.Log(FcuLocKey.log_generating_sprites.Localize(0, totalCount));
            monoBeh.EditorDelegateHolder.StartProgress?.Invoke(monoBeh, ProgressBarCategory.GeneratingSprites, totalCount, false);

            try
            {
                for (int i = 0; i < totalCount; i++)
                {
                    token.ThrowIfCancellationRequested();

                    fobject = generative[i];

                    _ = GenerateSprite(fobject, () =>
                    {
                        int currentCount = Interlocked.Increment(ref generatedCount);
                        monoBeh.EditorDelegateHolder.UpdateProgress?.Invoke(monoBeh, ProgressBarCategory.GeneratingSprites, currentCount);
                    });

                    await Task.Delay(250);
                }

                while (true)
                {
                    int currentCount = generatedCount;

                    if (loggedCount != currentCount)
                    {
                        loggedCount = currentCount;
                        Debug.Log(FcuLocKey.log_generating_sprites.Localize(currentCount, totalCount));
                    }

                    if (currentCount >= totalCount)
                        break;

                    await Task.Delay(1000, token);
                }
            }
            finally
            {
                monoBeh.EditorDelegateHolder.CompleteProgress?.Invoke(monoBeh, ProgressBarCategory.GeneratingSprites);
            }
        }

        private async Task GenerateSprite(FObject fobject, Action increase)
        {
            FcuLogger.Debug($"GenerateSprites | {fobject.Data.NameHierarchy} | {fobject.Data.NeedGenerate}", FcuDebugSettingsFlags.LogSpriteGenerator);

            try
            {
                byte[] bytes = FcuFigmageSpriteBaker.BakePngBytes(fobject, ResolveRenderScale());

                SpriteBatchWriter.Add(fobject, bytes);
            }
            catch (Exception ex)
            {
                FcuLogger.Debug($"Can't generate '{fobject.Data.NameHierarchy}'\n{ex}", FcuDebugSettingsFlags.LogError);
                fobject.Data.FcuImageType = FcuImageType.Drawable;
                fobject.SetReason(ReasonKey.Gen_GenerationFailed);
            }

            increase.Invoke();
            await Task.Yield();
        }

        private float ResolveRenderScale()
        {
            float scale = Mathf.Max(0.01f, monoBeh.Settings.ImageSpritesSettings.ImageScale);
            return Mathf.Round(scale * 100f) / 100f;
        }
    }
}