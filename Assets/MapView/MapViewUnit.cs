﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class MapViewUnit : MapViewObject, IMapViewSelectable, IMapViewSelfie
{
    public MapLogicUnit LogicUnit
    {
        get
        {
            return (MapLogicUnit)LogicObject;
        }
    }

    private MeshRenderer Renderer;
    private MeshFilter Filter;
    private Mesh ObstacleMesh;

    private GameObject ShadowObject;
    private MeshRenderer ShadowRenderer;
    private MeshFilter ShadowFilter;
    private Mesh ShadowMesh;

    // infowindow stuff
    private GameObject TexObject;
    private MeshRenderer TexRenderer;
    private Material TexMaterial;

    private Vector2 CurrentPoint;

    private bool DrawSelected = false;

    private Mesh UpdateMesh(Images.AllodsSpriteSeparate sprite, int frame, Mesh mesh, float shadowOffs, bool first)
    {
        Texture2D sTex = sprite.Frames[frame].Texture;
        float sW = sprite.Frames[frame].Width;
        float sH = sprite.Frames[frame].Height;
        float tMaxX = sW / sTex.width;
        float tMaxY = sH / sTex.height;

        bool flying = (LogicUnit.Template.MovementType == 3);

        float shadowOffsReal = shadowOffs * sH;
        float shadowOffsXLeft = -shadowOffsReal * (1f - LogicUnit.Class.CenterY);

        Vector3[] qv = new Vector3[4];
        int pp = 0;
        if (!flying || (shadowOffs == 0))
        {
            qv[pp++] = new Vector3(shadowOffsReal, 0, 0);
            qv[pp++] = new Vector3(shadowOffsReal + sW, 0, 0);
            qv[pp++] = new Vector3(shadowOffsXLeft + sW, sH, 0);
            qv[pp++] = new Vector3(shadowOffsXLeft, sH, 0);
        }
        else
        {
            float shadowOffsX = shadowOffsReal;
            shadowOffs = -4;
            qv[pp++] = new Vector3(shadowOffsX, shadowOffs, 0);
            qv[pp++] = new Vector3(shadowOffsX + sW, shadowOffs, 0);
            qv[pp++] = new Vector3(shadowOffsX + sW, shadowOffs + sH, 0);
            qv[pp++] = new Vector3(shadowOffsX, shadowOffs + sH, 0);
        }

        Vector2[] quv = new Vector2[4];
        quv[0] = new Vector2(0, 0);
        quv[1] = new Vector2(tMaxX, 0);
        quv[2] = new Vector2(tMaxX, tMaxY);
        quv[3] = new Vector2(0, tMaxY);

        mesh.vertices = qv;
        mesh.uv = quv;

        if (first)
        {
            Color[] qc = new Color[4];
            qc[0] = qc[1] = qc[2] = qc[3] = new Color(1, 1, 1, 1);
            mesh.colors = qc;

            int[] qt = new int[4];
            for (int i = 0; i < qt.Length; i++)
                qt[i] = i;
            mesh.SetIndices(qt, MeshTopology.Quads, 0);
        }

        Renderer.material.mainTexture = sTex;
        ShadowRenderer.material.mainTexture = sTex;

        return mesh;
    }

    public void Start()
    {
        if (LogicUnit.Class != null)
            name = string.Format("Unit (ID={0}, Class={1})", LogicUnit.ID, LogicUnit.Template.Name);
        else name = string.Format("Unit (ID={0}, Class=<INVALID>)", LogicUnit.ID);
        // let's give ourselves a sprite renderer first.
        Renderer = gameObject.AddComponent<MeshRenderer>();
        Renderer.enabled = false;
        Filter = gameObject.AddComponent<MeshFilter>();
        Filter.mesh = new Mesh();
        transform.localScale = new Vector3(1, 1, 1);

        ShadowObject = Utils.CreateObject();
        ShadowObject.name = "Shadow";
        ShadowObject.transform.parent = transform;
        ShadowRenderer = ShadowObject.AddComponent<MeshRenderer>();
        ShadowRenderer.enabled = false;
        ShadowFilter = ShadowObject.AddComponent<MeshFilter>();
        ShadowFilter.mesh = new Mesh();
        ShadowObject.transform.localScale = new Vector3(1, 1, 1);
        ShadowObject.transform.localPosition = new Vector3(0, 0, 16);
    }

    private bool spriteSet = false;
    private bool oldVisibility = false;
    public override void Update()
    {
        base.Update();

        if (LogicUnit.GetVisibility() != 2)
        {
            oldVisibility = false;
            Renderer.enabled = false;
            ShadowRenderer.enabled = false;
            return;
        }
        else if (!oldVisibility)
        {
            Renderer.enabled = true;
            ShadowRenderer.enabled = true;
            oldVisibility = true;
            return;
        }

        Renderer.material.SetFloat("_Lightness", DrawSelected ? 0.75f : 0.5f);

        if (LogicUnit.DoUpdateView)
        {
            Renderer.enabled = true;
            ShadowRenderer.enabled = true;

            Images.AllodsSpriteSeparate sprites = LogicUnit.Class.File.File;

            if (!spriteSet)
            {
                LogicUnit.Class.File.UpdateSprite();
                sprites = LogicUnit.Class.File.File;
                Renderer.material = new Material(MainCamera.MainShaderPaletted);
                //Renderer.material.SetTexture("_Palette", sprites.OwnPalette); // no palette swap for this one
                Renderer.material.SetTexture("_Palette", LogicUnit.Class.File.UpdatePalette(LogicUnit.Template.Face));
                ShadowRenderer.material = Renderer.material;
                ShadowRenderer.material.color = new Color(0, 0, 0, 0.5f);
                spriteSet = true;
            }

            int actualFrame = LogicUnit.Class.Index; // draw frame 0 of each unit
            Vector2 xP = MapView.Instance.MapToScreenCoords(LogicObject.X + (float)LogicObject.Width / 2, LogicObject.Y + (float)LogicObject.Height / 2, LogicUnit.Width, LogicUnit.Height);
            CurrentPoint = xP;
            transform.localPosition = new Vector3(xP.x - (float)sprites.Frames[actualFrame].Width * LogicUnit.Class.CenterX,
                                                    xP.y - (float)sprites.Frames[actualFrame].Height * LogicUnit.Class.CenterY,
                                                    MakeZFromY(xP.y)); // order sprites by y coordinate basically
            //Debug.Log(string.Format("{0} {1} {2}", xP.x, sprites.Sprites[0].rect.width, LogicUnit.Class.CenterX));
            //Renderer.sprite = sprites.Sprites[actualFrame];
            ObstacleMesh = UpdateMesh(sprites, actualFrame, Filter.mesh, 0, (ObstacleMesh == null));
            ShadowMesh = UpdateMesh(sprites, actualFrame, ShadowFilter.mesh, 0.3f, (ShadowMesh == null)); // 0.3 of sprite height

            LogicUnit.DoUpdateView = false;
        }
    }

    void OnDestroy()
    {
        if (Filter.mesh != null)
            DestroyImmediate(Filter.mesh, true);
        if (ShadowFilter.mesh != null)
            DestroyImmediate(ShadowFilter.mesh, true);
    }

    public bool IsSelected(int x, int y)
    {
        int cx = x - (int)CurrentPoint.x;
        int cy = y - (int)CurrentPoint.y;
        if (cx > LogicUnit.Class.SelectionX1 &&
            cx < LogicUnit.Class.SelectionX2 &&
            cy > LogicUnit.Class.SelectionY1 &&
            cy < LogicUnit.Class.SelectionY2)
        {
            DrawSelected = true;
            return true;
        }

        DrawSelected = false;
        return false;
    }

    public bool ProcessEventPic(Event e)
    {
        return false;
    }

    public bool ProcessEventInfo(Event e)
    {
        return false;
    }

    public void DisplayPic(bool on, Transform parent)
    {
        if (on)
        {
            // load infowindow texture.
            if (LogicUnit.Class.InfoPictureFile == null)
            {
                string picName = LogicUnit.Class.InfoPicture;
                if (LogicUnit.Template.Face > 1)
                    picName += LogicUnit.Template.Face.ToString();
                picName += ".bmp";
                LogicUnit.Class.InfoPictureFile = Images.LoadImage(picName, 0, Images.ImageType.AllodsBMP);
            }
            // init infowindow
            if (TexMaterial == null)
                TexMaterial = new Material(MainCamera.MainShader);
            TexObject = Utils.CreatePrimitive(PrimitiveType.Quad);
            TexRenderer = TexObject.GetComponent<MeshRenderer>();
            TexRenderer.material = TexMaterial;
            TexRenderer.material.mainTexture = LogicUnit.Class.InfoPictureFile;
            TexRenderer.enabled = true;
            TexRenderer.transform.parent = parent;
            TexRenderer.transform.localPosition = new Vector3((float)LogicUnit.Class.InfoPictureFile.width / 2 + 16,
                                                         (float)LogicUnit.Class.InfoPictureFile.height / 2 + 2, -0.01f);
            TexRenderer.transform.localScale = new Vector3(LogicUnit.Class.InfoPictureFile.width,
                                                           LogicUnit.Class.InfoPictureFile.height, 1);
        }
        else
        {
            Destroy(TexObject);
            TexObject = null;
            TexRenderer = null;
        }
    }

    public void DisplayInfo(bool on, Transform parent)
    {

    }
}