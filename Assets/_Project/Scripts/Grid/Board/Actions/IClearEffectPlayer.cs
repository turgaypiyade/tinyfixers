using System.Collections;

public interface IClearEffectPlayer
{
    bool CanPlay(IClearEffectDescriptor effect);
    IEnumerator Play(IClearEffectDescriptor effect, BoardController board, ClearEffectPlaybackContext context);
}