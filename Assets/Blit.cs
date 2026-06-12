using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class Blit : MonoBehaviour
{
   [SerializeField] private Material fractalMat;
   
   private RenderTexture rt;
   private void Start()
   {
      rt = new RenderTexture(2048, 2048, 0, RenderTextureFormat.ARGB32);
      CreateTexture();
   }

   private void CreateTexture()
   {
      Graphics.Blit(null, rt, fractalMat);
      
      Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
      RenderTexture.active = rt;
      tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
      tex.Apply();
      
      byte[] bytes = tex.EncodeToPNG();
      File.WriteAllBytes("Assets/Mandelbrot.png", bytes);
      AssetDatabase.Refresh();
   }
}
