using UnityEditor;
using UnityEngine;

namespace DeckDark.EditorTools
{
    /// <summary>Menu "Dark Deck" na barra do Unity.</summary>
    public static class DarkDeckMenu
    {
        [MenuItem("Dark Deck/Play with 3D Table")]
        static void Play3DTest()
        {
            PlayerPrefs.SetInt("deckdark_use3d", 1);
            PlayerPrefs.Save();
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }
    }
}
