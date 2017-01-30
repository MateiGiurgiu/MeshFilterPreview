using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MeshFilter))]
public class CustomMeshFilterPreview : Editor
{
    private enum UVchannels
    {
        UV0 = 0,
        UV1 = 1,
        UV2 = 2
    };

    private const float PAN_SPEED = 30.0f;
    private const float ZOOM_SPEED = 10.0f;
    private const float DRAG_SPEED = 10.0f;

    private PreviewRenderUtility meshPreviewUtility;
    private PreviewRenderUtility uvPreviewUtility;
    private MeshFilter targetMeshFilter;
    private MeshRenderer targetMeshRenderer;
    private Mesh targetMesh;

    private Vector2 drag;
    private Vector2 pan;
    private float zoom;

    private static bool showUVs = false;
    private float meshMaxSize;
    private Material defaultMaterial;
    private Material uvMaterial;
    private UVchannels UVchannel;

    #region Unity Calls

    private void OnEnable()
    {
        // getting components
        targetMeshFilter = target as MeshFilter;
        targetMeshRenderer = targetMeshFilter.GetComponent<MeshRenderer>();
        if (targetMeshFilter == null || targetMeshRenderer == null) return;
        targetMesh = targetMeshFilter.sharedMesh;

        // calculating bounds and real size
        targetMeshFilter.sharedMesh.RecalculateBounds();
        meshMaxSize = Mathf.Max(targetMesh.bounds.size.x, targetMesh.bounds.size.y, targetMesh.bounds.size.z);

        // initialising previewers 
        if (meshPreviewUtility == null)
        {
            meshPreviewUtility = new PreviewRenderUtility();
            meshPreviewUtility.m_Camera.transform.position = new Vector3(meshMaxSize, meshMaxSize / 2, meshMaxSize) * 5;
            meshPreviewUtility.m_Camera.transform.LookAt(Vector3.zero);
            meshPreviewUtility.m_Camera.farClipPlane = Mathf.Clamp(meshMaxSize * 20, 20, 1000);
        }

        if(uvPreviewUtility == null)
        {
            uvPreviewUtility = new PreviewRenderUtility();
            uvPreviewUtility.m_Camera.transform.position = new Vector3(0.5f, 1.0f, 0.5f);
            uvPreviewUtility.m_Camera.transform.rotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            uvPreviewUtility.m_Camera.nearClipPlane = 0.1f;
            uvPreviewUtility.m_Camera.orthographic = true;
            uvPreviewUtility.m_Camera.orthographicSize = 1;
        }

        defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
        uvMaterial = new Material(Shader.Find("Hidden/Wireframe"));
    }

    private void OnDestroy()
    {
        if(meshPreviewUtility != null)
        {
            meshPreviewUtility.Cleanup();
            meshPreviewUtility = null;
        }
        if(uvPreviewUtility != null)
        {
            uvPreviewUtility.Cleanup();
            uvPreviewUtility = null;
        }
    }

    public override bool HasPreviewGUI()
    {
        return true;
    }

    public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
    {
        // getting and applying user input
        Rect dragArea = r;
        dragArea.y += 20;
        dragArea.height -= 40;
        drag = Drag2D(dragArea, drag);
        zoom = Zoom(dragArea, zoom);
        pan = Pan2D(dragArea, pan);
        if (Event.current.type == EventType.Repaint)
        {
            if (showUVs)    DrawUVPreview(r, background);
            else            DrawMeshPreview(r, background);

            zoom = 0;
        }

        DrawUI(r, background);
    }

    #endregion


    #region Drawings

    private void DrawMeshPreview(Rect r, GUIStyle background)
    {
        if(targetMesh == null)
        {
            EditorGUI.DropShadowLabel(r, "Mesh Required");
        }
        else
        {
            // applying camera movement
            meshPreviewUtility.m_Camera.transform.RotateAround(Vector3.zero, Vector3.up, drag.x);
            meshPreviewUtility.m_Camera.transform.RotateAround(Vector3.zero, meshPreviewUtility.m_Camera.transform.right, drag.y);
            meshPreviewUtility.m_Camera.transform.position +=
                meshPreviewUtility.m_Camera.transform.forward * -zoom * meshMaxSize * ZOOM_SPEED +
                meshPreviewUtility.m_Camera.transform.right * -pan.x * meshMaxSize * PAN_SPEED +
                meshPreviewUtility.m_Camera.transform.up * -pan.y * meshMaxSize * PAN_SPEED;

            meshPreviewUtility.BeginPreview(r, background);

            // setting up the lights
            meshPreviewUtility.m_Light[0].intensity = 1.4f;
            meshPreviewUtility.m_Light[0].transform.rotation = Quaternion.Euler(60f, 0f, 60f);
            meshPreviewUtility.m_Light[0].type = LightType.Directional;
            meshPreviewUtility.m_Light[0].shadows = LightShadows.Hard;
            InternalEditorUtility.SetCustomLighting(meshPreviewUtility.m_Light, new Color(0.1f, 0.1f, 0.1f, 0f));

            // drawing the inspected mesh
            for (int i = 0; i < targetMesh.subMeshCount; i++)
            {
                Material mat;
                if (targetMeshRenderer.sharedMaterials.Length <= i)
                {
                    mat = defaultMaterial;
                }
                else
                {
                    mat = targetMeshRenderer.sharedMaterials[i];
                    if (mat == null)
                    {
                        mat = defaultMaterial;
                    }
                }

                meshPreviewUtility.DrawMesh(targetMesh, Matrix4x4.identity, mat, i);
            }

            meshPreviewUtility.m_Camera.Render();
            InternalEditorUtility.RemoveCustomLighting();
            meshPreviewUtility.EndAndDrawPreview(r);
        }
    }

    private void DrawUVPreview(Rect r, GUIStyle background)
    {
        if (targetMesh == null)
        {
            EditorGUI.DropShadowLabel(r, "Mesh Required");
        }
        else
        {
            // applying camera movement
            uvPreviewUtility.m_Camera.orthographicSize += zoom * ZOOM_SPEED;
            if (uvPreviewUtility.m_Camera.orthographicSize < 0.5)
                uvPreviewUtility.m_Camera.orthographicSize = 0.5f;

            uvPreviewUtility.m_Camera.transform.position +=
                uvPreviewUtility.m_Camera.transform.right * -pan.x * PAN_SPEED +
                uvPreviewUtility.m_Camera.transform.up * -pan.y * PAN_SPEED;

            // creating the box rect used for drawing the uv data into
            float boxSize = r.width < r.height ? r.width : r.height;
            boxSize -= 40;
            Rect boxRect = new Rect();
            boxRect.width = boxRect.height = boxSize;
            boxRect.center = r.center;
            boxRect.y = r.y + 30;

            // calculating preview render size used for making sure all the gui elements inside uvRenderPreview are alligned correctly
            float previewRenderSize = Mathf.Min(Mathf.Min(r.xMax, r.yMax), 1024);
            Rect previewRenderRect = new Rect(0.0f, 0.0f, previewRenderSize / 2, previewRenderSize / 2);

            // getting the current mesh data (uvs, vertices)
            int[] triangles = targetMeshFilter.sharedMesh.triangles;
            List<Vector2> uv = new List<Vector2>();
            targetMesh.GetUVs((int)UVchannel, uv);

            // do we have uv data on this channel?
            if (uv.Count == 0)
            {
                EditorGUI.DropShadowLabel(r, string.Format("No UV data on channel {0}", (int)UVchannel));
                return;
            }

            uvPreviewUtility.BeginPreview(previewRenderRect, background);

            string shaderKeyword = "UVCHANNEL_" + UVchannel.ToString();
            if (!uvMaterial.IsKeywordEnabled(shaderKeyword))
            {
                uvMaterial.DisableKeyword("UVCHANNEL_UV0");
                uvMaterial.DisableKeyword("UVCHANNEL_UV1");
                uvMaterial.DisableKeyword("UVCHANNEL_UV2");
                uvMaterial.EnableKeyword(shaderKeyword);
            }

            for (int i = 0; i < targetMesh.subMeshCount; i++)
            {
                uvPreviewUtility.DrawMesh(targetMesh, Matrix4x4.identity, uvMaterial, i);
            }

            // force camera to render
            uvPreviewUtility.m_Camera.Render();
            /**~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~**/
            Texture resultRender = uvPreviewUtility.EndPreview();
            GUI.DrawTexture(r, resultRender, ScaleMode.ScaleAndCrop, false);
            /**~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~**/
        }
    }

    private void DrawUI(Rect r, GUIStyle background)
    {
        // setting a cusomt style
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        style.fontSize = 10;

        Rect buttonMeshRect = new Rect(r.x, r.y, r.width / 2, EditorGUIUtility.singleLineHeight);
        Rect buttonUVRect = new Rect(r.x + r.width / 2, r.y, r.width / 2, EditorGUIUtility.singleLineHeight);

        if (GUI.Button(buttonMeshRect, "Mesh", EditorStyles.toolbarButton)) showUVs = false;
        if (GUI.Button(buttonUVRect, "UVs", EditorStyles.toolbarButton)) showUVs = true;

        if (!showUVs)
        {
            // mesh info
            Rect realSizeRect = new Rect(r.x, buttonMeshRect.yMax, r.width / 3, EditorGUIUtility.singleLineHeight);
            Rect pivotPositionRect = new Rect(r.x, realSizeRect.yMax, r.width / 3, EditorGUIUtility.singleLineHeight);
            Rect submeshesCountRect = new Rect(r.x, pivotPositionRect.yMax, r.width / 3, EditorGUIUtility.singleLineHeight);
            Rect trianglesCountRect = new Rect(r.x, submeshesCountRect.yMax, r.width / 3, EditorGUIUtility.singleLineHeight);
            Rect verticesCountRect = new Rect(r.x, trianglesCountRect.yMax, r.width / 3, EditorGUIUtility.singleLineHeight);
            GUI.Label(realSizeRect, string.Format("Real Size: [{0},{1},{2}]",
                (targetMesh.bounds.size.x).ToString("0.###"),
                (targetMesh.bounds.size.y).ToString("0.###"),
                (targetMesh.bounds.size.z).ToString("0.###")
            ));
            GUI.Label(pivotPositionRect, string.Format("Pivot: [{0},{1},{2}]",
                (targetMesh.bounds.center.x).ToString("0.####"),
                (targetMesh.bounds.center.y).ToString("0.####"),
                (targetMesh.bounds.center.z).ToString("0.####")
                ));
            GUI.Label(submeshesCountRect, string.Format("Submeshes: {0}", targetMeshFilter.sharedMesh.subMeshCount));
            GUI.Label(trianglesCountRect, string.Format("Triangles: {0}", targetMeshFilter.sharedMesh.triangles.Length / 3));
            GUI.Label(verticesCountRect, string.Format("Vertices: {0}", targetMeshFilter.sharedMesh.vertexCount));

            // mesh settings
            Rect centerPivotRect = new Rect(r.x, r.yMax - EditorGUIUtility.singleLineHeight, r.width / 6, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(centerPivotRect, "Center Pivot", EditorStyles.miniButton))
            {
                Bounds currentBounds = targetMeshFilter.sharedMesh.bounds;
                currentBounds.center = new Vector3(currentBounds.center.x, currentBounds.size.y / 2, currentBounds.center.z);
                targetMeshFilter.sharedMesh.bounds = currentBounds;
            }
        }
        else
        {
            // uv channel selection
            Rect uvUVchannelRect = new Rect(0, r.yMax - EditorGUIUtility.singleLineHeight, Mathf.Clamp(r.width / 10, 50, 100), EditorGUIUtility.singleLineHeight);
            UVchannel = (UVchannels)EditorGUI.EnumPopup(uvUVchannelRect, UVchannel);
        }
    }

    #endregion


    private void ResetMeshPreviewCamera()
    {
        if (meshPreviewUtility != null)
        {
            meshPreviewUtility.m_Camera.transform.position = new Vector3(meshMaxSize, meshMaxSize / 2, meshMaxSize) * 5;
            meshPreviewUtility.m_Camera.transform.LookAt(Vector3.zero);
        }
    }

    private void ResetUVPreviewCamera()
    {
        if(uvPreviewUtility != null)
        {
            uvPreviewUtility.m_Camera.transform.position = new Vector3(0.5f, 1.0f, 0.5f);
            uvPreviewUtility.m_Camera.orthographicSize = 1.0f;
        }
    }


    #region Control

    public static Vector2 Drag2D(Rect position, Vector2 scrollPosition)
    {
        int controlID = GUIUtility.GetControlID("MeshFilterPreview_Drag".GetHashCode(), FocusType.Passive);
        Event current = Event.current;
        switch (current.GetTypeForControl(controlID))
        {
            case EventType.Repaint:
                if (GUIUtility.hotControl == controlID && !showUVs)
                {
                    EditorGUIUtility.AddCursorRect(position, MouseCursor.Orbit);
                }
                break;
            case EventType.MouseDown:
                if (position.Contains(current.mousePosition) && position.width > 50f && current.button == 0)
                {
                    GUIUtility.hotControl = controlID;
                    current.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                    current.Use();
                    scrollPosition = Vector2.zero;
                }
                EditorGUIUtility.SetWantsMouseJumping(0);
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlID)
                {
                    scrollPosition = current.delta * (float)((!current.shift) ? 1 : 3) / Mathf.Min(position.width, position.height) * 140f;
                    scrollPosition.y = Mathf.Clamp(scrollPosition.y, -90f, 90f);
                    current.Use();
                    GUI.changed = true;
                }
                break;
        }
        return scrollPosition;
    }

    public static Vector2 Pan2D(Rect position, Vector2 pan)
    {
        int controlID = GUIUtility.GetControlID("MeshFilterPreview_Pan".GetHashCode(), FocusType.Passive);
        Event current = Event.current;
        switch (current.GetTypeForControl(controlID))
        {
            case EventType.Repaint:
                if (GUIUtility.hotControl == controlID)
                {
                    EditorGUIUtility.AddCursorRect(position, MouseCursor.Pan);
                }
                break;
            case EventType.MouseDown:
                if (position.Contains(current.mousePosition) && position.width > 50f && current.button == 2)
                {
                    GUIUtility.hotControl = controlID;
                    current.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                    current.Use();
                    pan = Vector2.zero;
                }
                EditorGUIUtility.SetWantsMouseJumping(0);
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlID)
                {
                    Vector2 delta = current.delta;
                    delta.y *= -1;
                    pan = delta * (float)((!current.shift) ? 1 : 3) / 10000;
                    current.Use();
                    GUI.changed = true;
                }
                break;
        }
        return pan;
    }

    public static float Zoom(Rect position, float currentZoom)
    {
        int controlID = GUIUtility.GetControlID("MeshFilterPreview_Zoom".GetHashCode(), FocusType.Passive);
        Event current = Event.current;
        switch (current.GetTypeForControl(controlID))
        {
            case EventType.ScrollWheel:
                if (position.Contains(current.mousePosition))
                {
                    currentZoom = current.delta.y * 0.01f;
                    current.Use();
                    GUI.changed = true;
                }
                break;
        }
        return currentZoom;
    }

    #endregion
}
