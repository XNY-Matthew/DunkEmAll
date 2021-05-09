using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.IO;

namespace MeshCombineStudio
{
    [CustomEditor(typeof(MeshCombiner))]
    [CanEditMultipleObjects]
    public class MeshCombinerEditor : Editor
    {
        enum VSyncCountMode { DontSync, EveryVBlank, EverySecondVBlank };
        VSyncCountMode vSyncCountMode;
        enum BoolEnum { On, Off };
        enum PropertyType { Property, Enum, Layer };

        GameObject meshCombine; 
        MeshCombiner meshCombiner;

        SearchConditions searchConditions = new SearchConditions();

        // Search Options
        public class SearchConditions
        {
            public SerializedProperty searchConditions, foldoutSearchConditions, foldoutSearchParents;
            public SerializedProperty searchParentGOs, objectCenter, lodGroupSearchMode, useSearchBox, searchBoxSquare, searchBoxPivot, searchBoxSize;
            public SerializedProperty useMaxBoundsFactor, maxBoundsFactor, useVertexInputLimit, vertexInputLimit;
            public SerializedProperty useLayerMask, layerMask, useTag, tag, useNameContains, nameContainList, onlyActive, onlyActiveMeshRenderers, onlyStatic;
            public SerializedProperty useComponentsFilter, componentCondition, componentNameList;

            public void Init(SerializedObject serializedObject)
            {
                searchConditions = serializedObject.FindProperty("searchOptions");
                foldoutSearchParents = searchConditions.FindPropertyRelative("foldoutSearchParents");
                foldoutSearchConditions = searchConditions.FindPropertyRelative("foldoutSearchConditions");
                objectCenter = searchConditions.FindPropertyRelative("objectCenter");
                lodGroupSearchMode = searchConditions.FindPropertyRelative("lodGroupSearchMode");
                useSearchBox = searchConditions.FindPropertyRelative("useSearchBox");
                searchBoxPivot = searchConditions.FindPropertyRelative("searchBoxPivot");
                searchBoxSize = searchConditions.FindPropertyRelative("searchBoxSize");
                searchBoxSquare = searchConditions.FindPropertyRelative("searchBoxSquare");
                searchParentGOs = searchConditions.FindPropertyRelative("parentGOs");

                useMaxBoundsFactor = searchConditions.FindPropertyRelative("useMaxBoundsFactor");
                maxBoundsFactor = searchConditions.FindPropertyRelative("maxBoundsFactor");
                useVertexInputLimit = searchConditions.FindPropertyRelative("useVertexInputLimit");
                vertexInputLimit = searchConditions.FindPropertyRelative("vertexInputLimit");
                useLayerMask = searchConditions.FindPropertyRelative("useLayerMask");
                layerMask = searchConditions.FindPropertyRelative("layerMask");
                useTag = searchConditions.FindPropertyRelative("useTag");
                tag = searchConditions.FindPropertyRelative("tag");
                onlyActive = searchConditions.FindPropertyRelative("onlyActive");
                onlyActiveMeshRenderers = searchConditions.FindPropertyRelative("onlyActiveMeshRenderers");
                onlyStatic = searchConditions.FindPropertyRelative("onlyStatic");
                useComponentsFilter = searchConditions.FindPropertyRelative("useComponentsFilter");
                componentCondition = searchConditions.FindPropertyRelative("componentCondition");
                componentNameList = searchConditions.FindPropertyRelative("componentNameList");
                useNameContains = searchConditions.FindPropertyRelative("useNameContains");
                nameContainList = searchConditions.FindPropertyRelative("nameContainList");
            }

            public void DrawSearchParents(MeshCombinerEditor parent, Color color)
            {
                GUIDraw.DrawHeader(foldoutSearchParents, new GUIContent("Search Parent GameObjects", "MCS will search inside the Parent GameObjects for meshes to combine."), color * parent.editorSkinMulti);

                if (!foldoutSearchParents.boolValue) { EditorGUILayout.EndVertical(); return; }
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(17);
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    searchParentGOs.InsertArrayElementAtIndex(Mathf.Max(searchParentGOs.arraySize - 1, 0));
                    searchParentGOs.GetArrayElementAtIndex(searchParentGOs.arraySize - 1).objectReferenceValue = null;
                }
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    if (searchParentGOs.arraySize > 1) searchParentGOs.DeleteArrayElementAtIndex(searchParentGOs.arraySize - 1);
                    else searchParentGOs.GetArrayElementAtIndex(0).objectReferenceValue = null;
                }
                if (GUILayout.Button("Add Selected"))
                {
                    for (int i = 0; i < Selection.gameObjects.Length; i++)
                    {
                        GameObject go = Selection.gameObjects[i];
                        searchParentGOs.InsertArrayElementAtIndex(Mathf.Max(searchParentGOs.arraySize - 1, 0));
                        // parent.serializedObject.ApplyModifiedProperties();
                        searchParentGOs.GetArrayElementAtIndex(searchParentGOs.arraySize - 1).objectReferenceValue = go;
                    }

                    CheckValidSearchParents(parent.meshCombiner);
                }
                if (GUILayout.Button("Clear"))
                {
                    searchParentGOs.arraySize = 1;
                    searchParentGOs.GetArrayElementAtIndex(0).objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);

                if (GUIDraw.PropertyArray(searchParentGOs, new GUIContent("Search Parents", "The GameObject parents that holds all meshes (as children) that need to be combined."), new GUIContent("GameObject"), true, true, true, true))
                {
                    CheckValidSearchParents(parent.meshCombiner);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }

            void CheckValidSearchParents(MeshCombiner meshCombiner)
            {
                HashSet<Transform> rootTransforms = new HashSet<Transform>();

                for (int i = 0; i < searchParentGOs.arraySize; i++)
                {
                    var go1 = (GameObject)searchParentGOs.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (go1 == null) continue;

                    Transform t1 = go1.transform;
                    if (t1.IsChildOf(meshCombiner.transform))
                    {
                        Debug.Log("(MeshCombineStudio) => This Mesh Combine Studio GameObject cannot be used as a Search Object for itself.");
                        searchParentGOs.DeleteArrayElementAtIndex(i--);
                        continue;
                    }


                    for (int j = 0; j < searchParentGOs.arraySize; j++)
                    {
                        if (j == i) continue;

                        var go2 = (GameObject)searchParentGOs.GetArrayElementAtIndex(j).objectReferenceValue;
                        if (go2 == null) continue;

                        if (t1.IsChildOf(go2.transform))
                        {
                            Debug.Log("(MeshCombineStudio) => " + go1.name + " is already part of the Search Parents. It's not possible to add a child of another Search Parent as that would lead to double combining.");
                            searchParentGOs.DeleteArrayElementAtIndex(i--);
                        }
                    }
                }
            }

            public void DrawSearchConditions(MeshCombinerEditor parent, Color color)
            {
                GUIDraw.DrawHeader(foldoutSearchConditions, new GUIContent("Search Conditions", "With Seach Conditions you can filter the GameObjects (with meshes) that will be combined."), color * parent.editorSkinMulti);

                if (!foldoutSearchConditions.boolValue) { EditorGUILayout.EndVertical(); return; }

                EditorGUI.indentLevel++;

                GUIDraw.PropertyField(objectCenter, new GUIContent("Object Center", "Which position should be used to determine the cell location."));
                GUIDraw.PropertyField(lodGroupSearchMode, new GUIContent("LODGroup Search Mode", "LodRenderers will search inside the LODGroup renderers, default search mode is LODGroup"));

                GUIDraw.PropertyField(onlyActive, new GUIContent("Only Active", "Only combine active GameObjects."));
                GUIDraw.PropertyField(onlyActiveMeshRenderers, new GUIContent("Only Active Mesh Renderers", "Only combine active Mesh Renderers."));
                GUIDraw.PropertyField(onlyStatic, new GUIContent("Only Static", "Only combine GameObjects that are marked as 'Static'."));
                // GUIDraw.DrawSpacer(0, 3, 0);
                GUIDraw.PropertyField(useSearchBox, new GUIContent("Use Search Box", "Only combine meshes that are within the bounds of the search box."));

                if (useSearchBox.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Search Box Pivot");
                    EditorGUI.indentLevel--;
                    EditorGUILayout.PropertyField(searchBoxPivot, GUIContent.none);
                    if (GUILayout.Button(new GUIContent("R", "Reset the Pivot position."), EditorStyles.miniButtonMid, GUILayout.Width(25))) searchBoxPivot.vector3Value = Vector3.zero;
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel++;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Search Box Size");
                    EditorGUI.indentLevel--;
                    EditorGUILayout.PropertyField(searchBoxSize, GUIContent.none);
                    if (GUILayout.Button(new GUIContent("R", "Reset the Size."), EditorStyles.miniButtonMid, GUILayout.Width(25))) searchBoxSize.vector3Value = Vector3.one;
                    EditorGUILayout.EndHorizontal();

                    GUIDraw.PropertyField(searchBoxSquare, new GUIContent("Search Box Square", "Make the search box bounds square."), true);
                }

                if ((CombineMode)parent.combineMode.enumValueIndex == CombineMode.StaticObjects)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(new GUIContent("Use Max Bounds Factor", "Only combine meshes which bounds are not bigger than x times the cell size."));
                    EditorGUILayout.PropertyField(useMaxBoundsFactor, GUIContent.none, GUILayout.Width(25));
                    if (useMaxBoundsFactor.boolValue)
                    {
                        GUI.changed = false;
                        EditorGUILayout.PropertyField(maxBoundsFactor, GUIContent.none);
                        if (GUI.changed)
                        {
                            if (maxBoundsFactor.floatValue < 1) maxBoundsFactor.floatValue = 1;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Use Vertex Input Limit", "Only combine meshes that don't exceed this vertex limit."));
                EditorGUILayout.PropertyField(useVertexInputLimit, GUIContent.none, GUILayout.Width(25));
                if (useVertexInputLimit.boolValue)
                {
                    EditorGUILayout.PropertyField(vertexInputLimit, GUIContent.none);
                }

                if (vertexInputLimit.intValue < 1) vertexInputLimit.intValue = 1;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Use LayerMask", "Only combine GameObjects which Layer is in this LayerMask."));
                EditorGUILayout.PropertyField(useLayerMask, GUIContent.none, GUILayout.Width(25));
                if (useLayerMask.boolValue)
                {
                    EditorGUILayout.PropertyField(layerMask, GUIContent.none);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Use Tag", "Only combine GameObjects which this tag."));
                EditorGUILayout.PropertyField(useTag, GUIContent.none, GUILayout.Width(25));
                if (useTag.boolValue)
                {
                    tag.stringValue = EditorGUILayout.TagField("", tag.stringValue);
                }
                EditorGUILayout.EndHorizontal();

                GUIDraw.PropertyField(useComponentsFilter, new GUIContent("Use Components Filter", "Only combine GameObjects with a certain component."));
                if (useComponentsFilter.boolValue)
                {
                    GUIDraw.PropertyField(componentCondition, new GUIContent("Condition", "And: Only include GameObjects that have all components.\nOr: Include GameObjects that have one of the components."), true);
                    EditorGUI.indentLevel++;
                    GUIDraw.PropertyArray(componentNameList, new GUIContent("Component Names"), new GUIContent("Name"));
                    EditorGUI.indentLevel--;
                }

                GUIDraw.PropertyField(useNameContains, new GUIContent("Use Name Contains", "Only combine GameObjects that with a certain name."));
                if (useNameContains.boolValue)
                {
                    EditorGUI.indentLevel++;
                    GUIDraw.PropertyArray(nameContainList, new GUIContent("Names"), new GUIContent("Name"));
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        CombineConditions combineConditions = new CombineConditions();

        // Separate Options
        public class CombineConditions
        {
            public SerializedProperty combineConditionSettings, combineCondition, foldout;
            public SerializedProperty sameMaterial;
            public SerializedProperty sameShadowCastingMode, sameReceiveShadows;
#if !UNITY_2017 && !UNITY_2018 && !UNITY_2019_1
            public SerializedProperty sameReceiveGI;
#endif
            public SerializedProperty sameLightProbeUsage, sameReflectionProbeUsage, sameProbeAnchor;
            public SerializedProperty sameMotionVectorGenerationMode, sameStaticEditorFlags, sameLayer;

            public SerializedProperty material;
            public SerializedProperty shadowCastingMode, receiveShadows, receiveGI;
            public SerializedProperty lightProbeUsage, reflectionProbeUsage, probeAnchor;
            public SerializedProperty motionVectorGenerationMode, layer, staticEditorFlags;

            public void Init(SerializedObject serializedObject)
            {
                combineConditionSettings = serializedObject.FindProperty("combineConditionSettings");
                combineCondition = combineConditionSettings.FindPropertyRelative("combineCondition");
                foldout = combineConditionSettings.FindPropertyRelative("foldout");

                sameMaterial = combineConditionSettings.FindPropertyRelative("sameMaterial");

                sameShadowCastingMode = combineConditionSettings.FindPropertyRelative("sameShadowCastingMode");
                sameReceiveShadows = combineConditionSettings.FindPropertyRelative("sameReceiveShadows");
#if !UNITY_2017 && !UNITY_2018 && !UNITY_2019_1
                sameReceiveGI = combineConditionSettings.FindPropertyRelative("sameReceiveGI");
#endif

                sameLightProbeUsage = combineConditionSettings.FindPropertyRelative("sameLightProbeUsage");
                sameReflectionProbeUsage = combineConditionSettings.FindPropertyRelative("sameReflectionProbeUsage");
                sameProbeAnchor = combineConditionSettings.FindPropertyRelative("sameProbeAnchor");

                sameMotionVectorGenerationMode = combineConditionSettings.FindPropertyRelative("sameMotionVectorGenerationMode");
                sameStaticEditorFlags = combineConditionSettings.FindPropertyRelative("sameStaticEditorFlags");
                sameLayer = combineConditionSettings.FindPropertyRelative("sameLayer");

                material = combineConditionSettings.FindPropertyRelative("material");
                shadowCastingMode = combineCondition.FindPropertyRelative("shadowCastingMode");
                receiveShadows = combineCondition.FindPropertyRelative("receiveShadows");
#if !UNITY_2017 && !UNITY_2018 && !UNITY_2019_1
                receiveGI = combineCondition.FindPropertyRelative("receiveGI");
#endif

                lightProbeUsage = combineCondition.FindPropertyRelative("lightProbeUsage");
                reflectionProbeUsage = combineCondition.FindPropertyRelative("reflectionProbeUsage");
                probeAnchor = combineCondition.FindPropertyRelative("probeAnchor");

                motionVectorGenerationMode = combineCondition.FindPropertyRelative("motionVectorGenerationMode");
                staticEditorFlags = combineCondition.FindPropertyRelative("staticEditorFlags");
                layer = combineCondition.FindPropertyRelative("layer");
            }

            void DrawCombineCondition(SerializedProperty boolProperty, SerializedProperty property, GUIContent guiContent, bool indent, PropertyType propertyType = PropertyType.Property)
            {
                EditorGUILayout.BeginHorizontal();
                if (indent) EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(guiContent);
                if (indent) EditorGUI.indentLevel--;
                bool value = !boolProperty.boolValue;

                GUI.changed = false;
                value = EditorGUILayout.Toggle(GUIContent.none, value, GUILayout.Width(25));
                if (GUI.changed)
                {
                    boolProperty.boolValue = !value;
                }
                

                if (!boolProperty.boolValue)
                {
                    if (propertyType == PropertyType.Property)
                    {
                        EditorGUILayout.PropertyField(property, GUIContent.none);
                    }
                    else if (propertyType == PropertyType.Enum)
                    {
                        var boolEnum = property.boolValue ? BoolEnum.On : BoolEnum.Off;
                        boolEnum = (BoolEnum)EditorGUILayout.EnumPopup(boolEnum);
                        property.boolValue = (boolEnum == BoolEnum.On ? true : false);
                    }
                    else
                    {
                        layer.intValue = EditorGUILayout.LayerField(layer.intValue);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            public void Draw(MeshCombinerEditor parent, Color color)
            {
                GUIDraw.DrawHeader(foldout, new GUIContent("Combine Conditions", "The conditions for meshes to be combinable.\n\nCombining can be separated by category. E.g. Only combine meshes that have shadow casting 'On' and separately combine meshes that have shadows 'Off'.\n\nIf you select a 'Change' setting, the origal setting will be ignored and the combined mesh with have the output setting you choose."), color * parent.editorSkinMulti);

                if (!foldout.boolValue) { EditorGUILayout.EndVertical(); return; }

                EditorGUI.indentLevel++;

                DrawCombineCondition(sameMaterial, material, new GUIContent("Change Materials", "Ignore source 'Materials' and output the selected material."), true);

                GUIDraw.Label("Lighting", 12);
                DrawCombineCondition(sameShadowCastingMode, shadowCastingMode, new GUIContent("Change Cast Shadows", "Ignore source 'Cast Shadows' and output the selected."), true);
                DrawCombineCondition(sameReceiveShadows, receiveShadows, new GUIContent("Change Receive Shadows", "Ignore source 'Receive Shadows' and output the selected."), true, PropertyType.Enum);
#if !UNITY_2017 && !UNITY_2018 && !UNITY_2019_1
                DrawCombineCondition(sameReceiveGI, receiveGI, new GUIContent("Change Receive GI", "Ignore source 'Receive GI' and output the selected."), true);
#endif

                GUIDraw.Label("Probes", 12);
                DrawCombineCondition(sameLightProbeUsage, lightProbeUsage, new GUIContent("Change Light Probes", "Ignore source 'Light Probes' and output the selected.'"), true);
                DrawCombineCondition(sameReflectionProbeUsage, reflectionProbeUsage, new GUIContent("Change Reflection Probes", "Ignore source 'Reflection Probes' and output the selected."), true);
                DrawCombineCondition(sameProbeAnchor, probeAnchor, new GUIContent("Change Anchor Override", "Ignore source 'Receive Shadows' and output the selected."), true);

                GUIDraw.Label("Additional", 12);
                DrawCombineCondition(sameMotionVectorGenerationMode, motionVectorGenerationMode, new GUIContent("Change Motion Vectors", "Ignore source 'Motion Vectors' and output the selected."), true);
                DrawCombineCondition(sameStaticEditorFlags, staticEditorFlags, new GUIContent("Change Static Flags", "Ignore source 'Static Flags' and output the selected."), true);
                //            EditorGUILayout.BeginHorizontal();
                //            EditorGUILayout.PrefixLabel(new GUIContent("Static", "The combined GameObjects will have these static settings."));
                //#if UNITY_5 || UNITY_2017_1 || UNITY_2017_2

                //            outputStatic.intValue = (int)(StaticEditorFlags)(EditorGUILayout.EnumMaskField(GUIContent.none, (StaticEditorFlags)outputStatic.intValue));
                //#else
                //            outputStatic.intValue = (int)(StaticEditorFlags)(EditorGUILayout.EnumFlagsField(GUIContent.none, (StaticEditorFlags)outputStatic.intValue));
                //#endif
                //            EditorGUILayout.EndHorizontal();

                if ((staticEditorFlags.intValue & (int)StaticEditorFlags.BatchingStatic) != 0)
                {
                    Debug.Log("(MeshCombineStudio) => Batching Static cannot be used, because MCS replaces Unity's Static batching and it would result in double combining giving wrong results.");
                    staticEditorFlags.intValue &= (int)~StaticEditorFlags.BatchingStatic;
                }

                DrawCombineCondition(sameLayer, layer, new GUIContent("Change Layer", "Ignore source 'Layer' and output the selected."), true, PropertyType.Layer);

                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
            }
        }

        public SerializedProperty drawGizmos, drawMeshBounds;

        // Unity Settings
        SerializedProperty unitySettingsFoldout;

        // Output Settings
        SerializedProperty outputSettingsFoldout;
        SerializedProperty combineMode, cellSize, cellOffset, useVertexOutputLimit, vertexOutputLimit, makeMeshesUnreadable;
        SerializedProperty addMeshColliders, addMeshCollidersInRange, addMeshCollidersBounds;
#if MCSCaves
        SerializedProperty removeOverlappingTriangles, removeSamePositionTriangles, overlapLayerMask, voxelizeLayer, lodGroupLayer;
        SerializedProperty overlappingNonCombineGO, disableOverlappingNonCombineGO;
#endif
        SerializedProperty removeTrianglesBelowSurface, noColliders, surfaceLayerMask, maxSurfaceHeight;
        SerializedProperty removeBackFaceTriangles, backFaceTriangleMode, backFaceDirection, backFaceBounds, twoSidedShadows;
        SerializedProperty weldVertices, weldSnapVertices, weldSnapSize, weldIncludeNormals;
        SerializedProperty scaleInLightmap;
        SerializedProperty copyBakedLighting, validCopyBakedLighting, rebakeLighting, rebakeLightingMode;

        // JoSettings
        SerializedProperty jobSettingsFoldout;
        SerializedProperty jobSettings, combineJobMode, combineMeshesPerFrame, threadAmountMode, customThreadAmount;
        SerializedProperty useMultiThreading, useMainThread, showStats;

        // Runtime
        SerializedProperty runtimeSettingsFoldout;
        SerializedProperty combineInRuntime, combineOnStart, useCombineSwapKey, originalMeshRenderers, originalLODGroups; // combineSwapKey
        
        SerializedProperty activeOriginal;
        
        SerializedObject jobManagerSerializedObject;

        float editorSkinMulti;
        bool repaint = false;

        private void OnEnable()
        {
            editorSkinMulti = EditorGUIUtility.isProSkin ? 1 : 0.35f;

            meshCombiner = (MeshCombiner)target;
            Transform t = meshCombiner.transform;
            t.hideFlags = HideFlags.HideInInspector;

            drawGizmos = serializedObject.FindProperty("drawGizmos");
            drawMeshBounds = serializedObject.FindProperty("drawMeshBounds");

            // Unity Settings
            unitySettingsFoldout = serializedObject.FindProperty("unitySettingsFoldout");

            // Search Conditions
            searchConditions.Init(serializedObject);

            // Combine Conditions
            combineConditions.Init(serializedObject);

            // Output Settings
            outputSettingsFoldout = serializedObject.FindProperty("outputSettingsFoldout");
            combineMode = serializedObject.FindProperty("combineMode");
            cellSize = serializedObject.FindProperty("cellSize");
            cellOffset = serializedObject.FindProperty("cellOffset");

            addMeshColliders = serializedObject.FindProperty("addMeshColliders");
            addMeshCollidersInRange = serializedObject.FindProperty("addMeshCollidersInRange");
            addMeshCollidersBounds = serializedObject.FindProperty("addMeshCollidersBounds");

            makeMeshesUnreadable = serializedObject.FindProperty("makeMeshesUnreadable");
            useVertexOutputLimit = serializedObject.FindProperty("useVertexOutputLimit");
            vertexOutputLimit = serializedObject.FindProperty("vertexOutputLimit");
            copyBakedLighting = serializedObject.FindProperty("copyBakedLighting");
            validCopyBakedLighting = serializedObject.FindProperty("validCopyBakedLighting");
            rebakeLighting = serializedObject.FindProperty("rebakeLighting");
            rebakeLightingMode = serializedObject.FindProperty("rebakeLightingMode");
            scaleInLightmap = serializedObject.FindProperty("scaleInLightmap");

            weldVertices = serializedObject.FindProperty("weldVertices");
            weldSnapVertices = serializedObject.FindProperty("weldSnapVertices");
            weldSnapSize = serializedObject.FindProperty("weldSnapSize");
            weldIncludeNormals = serializedObject.FindProperty("weldIncludeNormals");

#if MCSCaves
            removeOverlappingTriangles = serializedObject.FindProperty("removeOverlappingTriangles");
            removeSamePositionTriangles = serializedObject.FindProperty("removeSamePositionTriangles");
            overlapLayerMask = serializedObject.FindProperty("overlapLayerMask");
            voxelizeLayer = serializedObject.FindProperty("voxelizeLayer");
            lodGroupLayer = serializedObject.FindProperty("lodGroupLayer");

            overlappingNonCombineGO = serializedObject.FindProperty("overlappingNonCombineGO");
            disableOverlappingNonCombineGO = serializedObject.FindProperty("disableOverlappingNonCombineGO");
#endif

            removeTrianglesBelowSurface = serializedObject.FindProperty("removeTrianglesBelowSurface");
            noColliders = serializedObject.FindProperty("noColliders");
            surfaceLayerMask = serializedObject.FindProperty("surfaceLayerMask");
            maxSurfaceHeight = serializedObject.FindProperty("maxSurfaceHeight");

            removeBackFaceTriangles = serializedObject.FindProperty("removeBackFaceTriangles");
            backFaceTriangleMode = serializedObject.FindProperty("backFaceTriangleMode");
            backFaceDirection = serializedObject.FindProperty("backFaceDirection");
            backFaceBounds = serializedObject.FindProperty("backFaceBounds");
            twoSidedShadows = serializedObject.FindProperty("twoSidedShadows");

            // Job Settings
            jobSettingsFoldout = serializedObject.FindProperty("jobSettingsFoldout");

            // Runtime Settings
            runtimeSettingsFoldout = serializedObject.FindProperty("runtimeSettingsFoldout");
            combineInRuntime = serializedObject.FindProperty("combineInRuntime");
            combineOnStart = serializedObject.FindProperty("combineOnStart");
            useCombineSwapKey = serializedObject.FindProperty("useCombineSwapKey");
            // combineSwapKey = serializedObject.FindProperty("combineSwapKey");
            originalMeshRenderers = serializedObject.FindProperty("originalMeshRenderers");
            originalLODGroups = serializedObject.FindProperty("originalLODGroups");

            activeOriginal = serializedObject.FindProperty("activeOriginal");

            jobSettings = serializedObject.FindProperty("jobSettings");

            combineJobMode = jobSettings.FindPropertyRelative("combineJobMode");
            combineMeshesPerFrame = jobSettings.FindPropertyRelative("combineMeshesPerFrame");
            threadAmountMode = jobSettings.FindPropertyRelative("threadAmountMode");
            customThreadAmount = jobSettings.FindPropertyRelative("customThreadAmount");
            useMultiThreading = jobSettings.FindPropertyRelative("useMultiThreading");
            useMainThread = jobSettings.FindPropertyRelative("useMainThread");
            showStats = jobSettings.FindPropertyRelative("showStats");

            if (meshCombiner.instantiatePrefab == null) SetInstantiatePrefabReference();

            meshCombiner.InitData();
        }

        void SetInstantiatePrefabReference()
        {
            string path = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(meshCombiner));
            path = path.Replace("/Scripts/Mesh/MeshCombiner.cs", "/Sources/InstantiatePrefab.prefab");

            meshCombiner.instantiatePrefab = (GameObject)AssetDatabase.LoadAssetAtPath(path, typeof(GameObject));
        }

        private void OnDisable()
        {
            Tools.hidden = false;
        }
        
        void OnSceneGUI()
        {
            ApplyTransformLock();

            if (Tools.current == Tool.Rotate || Tools.current == Tool.Move || Tools.current == Tool.Scale) Tools.hidden = true;
            else { Tools.hidden = false; return; }

            // serializedObject.Update();

            if (meshCombiner.removeBackFaceTriangles)
            {
                Bounds bounds = meshCombiner.backFaceBounds;

                if (meshCombiner.backFaceTriangleMode == MeshCombiner.BackFaceTriangleMode.Direction)
                {
                    Quaternion rot = Handles.RotationHandle(Quaternion.Euler(meshCombiner.backFaceDirection), bounds.center);
                    meshCombiner.backFaceDirection = rot.eulerAngles;
                    Handles.color = new Color(0.19f, 0.4f, 898f, 1);

                    Handles.ArrowHandleCap(0, bounds.center, rot, HandleUtility.GetHandleSize(bounds.center), EventType.Repaint);

                    Handles.color = Color.white;
                }
                else
                {
                    if (Tools.current == Tool.Move)
                    {
                        bounds.center = Handles.PositionHandle(bounds.center, Quaternion.identity);
                    }
                    else if (Tools.current == Tool.Scale)
                    {
                        bounds.size = Handles.ScaleHandle(bounds.size, bounds.center, Quaternion.identity, HandleUtility.GetHandleSize(bounds.center));
                        bounds.size = Mathw.SetMin(bounds.size, 0.001f);
                    }
                    meshCombiner.backFaceBounds = bounds;
                }
                Handles.color = new Color(0, 0, 1, 0.15f);
                Handles.DrawSolidDisc(bounds.center, Vector3.up, HandleUtility.GetHandleSize(bounds.center) * 0.33f);
                Handles.color = Color.white;
            }

            MeshCombiner.SearchOptions searchOptions = meshCombiner.searchOptions;

            if (searchOptions.useSearchBox)
            {
                if (Tools.current == Tool.Move)
                {
                    meshCombiner.searchOptions.searchBoxPivot = Handles.PositionHandle(searchOptions.searchBoxPivot, Quaternion.identity);
                }
                else if (Tools.current == Tool.Scale)
                {
                    searchOptions.searchBoxSize = Handles.ScaleHandle(searchOptions.searchBoxSize, searchOptions.searchBoxPivot, Quaternion.identity, HandleUtility.GetHandleSize(searchOptions.searchBoxPivot));
                }
                Handles.color = new Color(1, 0, 0, 0.15f);
                Handles.DrawSolidDisc(searchOptions.searchBoxPivot, Vector3.up, HandleUtility.GetHandleSize(searchOptions.searchBoxPivot) * 0.33f);
                Handles.color = Color.white;

                ApplyScaleLimit();
            }

            if (meshCombiner.addMeshColliders && meshCombiner.addMeshCollidersInRange)
            {
                Bounds bounds = meshCombiner.addMeshCollidersBounds;

                if (Tools.current == Tool.Move)
                {
                    bounds.center = Handles.PositionHandle(bounds.center, Quaternion.identity);
                }
                else if (Tools.current == Tool.Scale)
                {
                    bounds.size = Handles.ScaleHandle(bounds.size, bounds.center, Quaternion.identity, HandleUtility.GetHandleSize(bounds.center));
                    bounds.size = Mathw.SetMin(bounds.size, 0.001f);
                }
                meshCombiner.addMeshCollidersBounds = bounds;

                Handles.color = new Color(0, 0, 1, 0.15f);
                Handles.DrawSolidDisc(bounds.center, Vector3.up, HandleUtility.GetHandleSize(bounds.center) * 0.33f);
                Handles.color = Color.white;
            }
            //serializedObject.ApplyModifiedProperties();
        }

        public override bool UseDefaultMargins()
        {
            return false;
        }

        public override void OnInspectorGUI()
        {
            meshCombiner = (MeshCombiner)target;

#if !UNITY_2017 && !UNITY_2018_1 && !UNITY_2018_2
            if (Event.current.type == EventType.Repaint && PrefabUtility.IsPartOfAnyPrefab(target) && PrefabUtility.GetPrefabInstanceStatus(target) != PrefabInstanceStatus.NotAPrefab)
            {
                PrefabUtility.UnpackPrefabInstance(meshCombiner.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                return;
            }
#else
            if (Event.current.type == EventType.Repaint && PrefabUtility.GetPrefabType(target) == PrefabType.PrefabInstance)
            {
                PrefabUtility.DisconnectPrefabInstance(meshCombiner.gameObject);
            }
#endif

            // DrawDefaultInspector();
            if (meshCombiner.lodGroupsSettings == null || meshCombiner.lodGroupsSettings.Length != 8) meshCombiner.CreateLodGroupsSettings();

            DrawInspectorGUI();
        }

        void DrawInspectorGUI()
        {
            GUILayout.Space(5);
            float space = 0;
            GUIDraw.DrawSpacer(0, 5, space);

            // EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = new Color(0.35f, 1, 0.35f);
            if (GUILayout.Button("Open Documentation"))
            {
                Application.OpenURL("http://www.terraincomposer.com/mcs-documentation/");
            }
            GUI.backgroundColor = Color.white;
            // EditorGUILayout.EndVertical();

            GUIDraw.DrawSpacer(space, 5, space);
                DrawUnitySettings(Color.white);
            serializedObject.ApplyModifiedProperties();
            GUIDraw.DrawSpacer(space, 5, space);
                searchConditions.DrawSearchParents(this, Color.red);
            GUIDraw.DrawSpacer(space, 5, space);
                searchConditions.DrawSearchConditions(this, Color.red);
            GUIDraw.DrawSpacer(space, 5, space);
                combineConditions.Draw(this, Color.blue);
            GUIDraw.DrawSpacer(space, 5, space);
                DrawOutputSettings(Color.blue);
            GUIDraw.DrawSpacer(space, 5, space);

            meshCombiner.InitMeshCombineJobManager();
            if (MeshCombineJobManager.instance != null) DrawJobSettings();
            
            GUIDraw.DrawSpacer(space, 5, space);
                DrawRuntime(Color.green);
            GUIDraw.DrawSpacer(space, 5, space);
                DrawCombining();

            if (repaint)
            {
                repaint = false;
                Repaint();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawUnitySettings(Color color)
        {
            bool staticBatching;
            bool dynamicBatching;

            GetBatching(out staticBatching, out dynamicBatching);

            GUIDraw.DrawHeader(unitySettingsFoldout, new GUIContent("Unity Settings", "Unity's batching settings from Player Settings.\nUnity's V sync Count settings from Quality Settings."), color);

            if (!unitySettingsFoldout.boolValue) { EditorGUILayout.EndVertical(); return; }

            EditorGUI.indentLevel++;

            GUI.changed = false;
            if (staticBatching)
            {
                if (combineInRuntime.boolValue)
                {
                    // GUI.backgroundColor = new Color(Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 5)), 0, 0, 1);
                    EditorGUILayout.HelpBox("Make sure that Unity's Static Batching doesn't get applied (with Batching Static flag) to objects you want to combine at runtime, as it will override MCS combining and it would be combined twice, giving wrong results.", MessageType.Warning, true);
                    // repaint = true;
                }
                // else EditorGUILayout.HelpBox("Unity's static batching can be used for meshes that are not combined with MCS", MessageType.Warning, true);
            }

            staticBatching = GUIDraw.Toggle(staticBatching, new GUIContent("Static Batching"));
            GUI.backgroundColor = Color.white;
            dynamicBatching = GUIDraw.Toggle(dynamicBatching, new GUIContent("Dynamic Batching"));
            if (GUI.changed) SetBatchingActive(staticBatching, dynamicBatching);

            vSyncCountMode = (VSyncCountMode)QualitySettings.vSyncCount;

            if (vSyncCountMode != VSyncCountMode.DontSync)
            {
                EditorGUILayout.HelpBox("Put 'V Sync Count' to 'Don't Sync' to see true fps to measure performance difference. Otherwise FPS will be capped to 60 fps.", MessageType.Warning, true);
            }

            GUI.changed = false;
            vSyncCountMode = (VSyncCountMode)GUIDraw.EnumPopup(vSyncCountMode, new GUIContent("V Sync Count"));
            if (GUI.changed) QualitySettings.vSyncCount = (int)vSyncCountMode;

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        void DrawOutputSettings(Color color)
        {
            GUIDraw.DrawHeader(outputSettingsFoldout, new GUIContent("Output Settings", "The settings for the combined meshes."), color * editorSkinMulti);

            if (!outputSettingsFoldout.boolValue) { EditorGUILayout.EndVertical(); return; }

            EditorGUI.indentLevel++;

            GUIDraw.PropertyField(combineMode, new GUIContent("Combine Mode",
                "StaticObjects => Combine Static Objects cell based.\n\n" +
                "DynamicObjects => Combine Dynamic GameObjects. The dynamic object/parts need to be marked with the 'MCS Dynamic Object' script."));
                //"Dynamic => Combine dynamic GameObject where the combined GameObject will be children of the original GameObjects."));

            if ((CombineMode)combineMode.enumValueIndex == CombineMode.StaticObjects)
            {
                GUI.changed = false;
                int oldCellSize = cellSize.intValue;
                GUIDraw.PropertyField(cellSize, new GUIContent("Cell Size", "Meshes within a cell will be combined together."), true);
                if (GUI.changed)
                {
                    if (cellSize.intValue < 4) cellSize.intValue = 4;
                    if (oldCellSize != cellSize.intValue)
                    {
                        float ratio = (float)cellSize.intValue / oldCellSize;
                        cellOffset.vector3Value *= ratio;
                        
                        // if (meshCombiner.octreeContainsObjects) meshCombiner.ResetOctree();
                    }
                }
                EditorGUILayout.BeginHorizontal();
                GUI.changed = false;
                EditorGUI.indentLevel++;
                EditorGUILayout.PrefixLabel(new GUIContent("Cell Offset", "Offset position of the cells."));
                EditorGUI.indentLevel--;
                EditorGUILayout.PropertyField(cellOffset, GUIContent.none);
                int halfCellSize = cellSize.intValue / 2;
                if (GUI.changed)
                {
                    Vector3 cellOffsetValue = cellOffset.vector3Value;
                    if (cellOffsetValue.x > halfCellSize) cellOffsetValue.x = halfCellSize;
                    else if (cellOffsetValue.x < 0) cellOffsetValue.x = 0;
                    if (cellOffsetValue.y > halfCellSize) cellOffsetValue.y = halfCellSize;
                    else if (cellOffsetValue.y < 0) cellOffsetValue.y = 0;
                    if (cellOffsetValue.z > halfCellSize) cellOffsetValue.z = halfCellSize;
                    else if (cellOffsetValue.z < 0) cellOffsetValue.z = 0;
                    cellOffset.vector3Value = cellOffsetValue;
                }
                if (GUILayout.Button(new GUIContent("H", "Offset by half the cell size"), EditorStyles.miniButtonMid, GUILayout.Width(25))) cellOffset.vector3Value = new Vector3(halfCellSize, halfCellSize, halfCellSize);
                if (GUILayout.Button(new GUIContent("R", "Reset the cell offset"), EditorStyles.miniButtonMid, GUILayout.Width(25))) cellOffset.vector3Value = Vector3.zero;
                EditorGUILayout.EndHorizontal();
            }

            // GUIDraw.Label("Removal", 12);
#if MCSCaves
            GUIDraw.PropertyField(removeOverlappingTriangles, new GUIContent("Remove Overlapping Tris", "Remove triangles that are overlapping inside a mesh volume.\n\nThe mesh volumes that are checked against need to be closed."));

            if (removeOverlappingTriangles.boolValue)
            {
                if (combineJobMode.enumValueIndex == (int)MeshCombineJobManager.CombineJobMode.CombineAtOnce)
                {
                    Debug.Log("(MeshCombineStudio) => For `Remove Overlapping Tris` mode combining per frame is auto selected to see the progression");
                    combineJobMode.enumValueIndex = (int)MeshCombineJobManager.CombineJobMode.CombinePerFrame;
                }

                GUIDraw.PropertyField(removeSamePositionTriangles, new GUIContent("Remove Same Position Tris", "Remove triangles that share the same position inside a mesh volume.\nE.g. this will remove insides of cubes that are snapped together."), true);
                GUIDraw.PropertyField(overlappingNonCombineGO, new GUIContent("Removal Helper Parent", "You can use additional meshes that get included in overlapping geometry removal, but without combining those meshes. E.g. like rocks sticking through a volume where they shouldn't.\n\nThe meshes need to be a child of the Removal Helper GameObject Parent and they need to be on the Overlap LayerMask."), true);
                if (overlappingNonCombineGO.objectReferenceValue != null)
                {
                    EditorGUI.indentLevel++;
                    GUIDraw.PropertyField(disableOverlappingNonCombineGO, new GUIContent("Disable After Combining", "Disable the Removal Helper Parent after combining."), true, 2);
                }
                GUIDraw.PropertyField(overlapLayerMask, new GUIContent("Overlap LayerMask"), true);
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Select 2 free layers that have no colliders on them", MessageType.Info, true);
                EditorGUI.indentLevel--;
                GUIDraw.LayerField(voxelizeLayer, new GUIContent("Free Layer 1", "An unused layer is needed to voxelize the meshes."), true);
                if (voxelizeLayer.intValue == lodGroupLayer.intValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox("Free Layer 1 and Free Layer 2 cannot be the same, please select another free layer that has no active colliders on it", MessageType.Error, true);
                    EditorGUI.indentLevel--;
                }
                GUIDraw.LayerField(lodGroupLayer, new GUIContent("Free Layer 2", "An unused layer is needed to voxelize the meshes."), true);
            }
#endif

            GUIDraw.PropertyField(removeTrianglesBelowSurface, new GUIContent("Remove Tris Below Surface", "Remove triangles below any surface (terrain and or meshes)."));

            if (removeTrianglesBelowSurface.boolValue)
            {
                GUIDraw.PropertyField(noColliders, new GUIContent("Surface Has No Colliders", "Choose this option if your surface/s doesn't have colliders or if the colliders are not accurate."), true);
                GUIDraw.PropertyField(surfaceLayerMask, new GUIContent("Surface LayerMask", "The layer of your surface/s."), true);
                GUIDraw.PropertyField(maxSurfaceHeight, new GUIContent("Raycast Height", "This needs to be at least the maximum height of your surface/s."), true);
            }

            GUIDraw.PropertyField(removeBackFaceTriangles, new GUIContent("Remove Backface Tris", "This can be used if the camera position is limited within an area or if the camera is limited in direction and movement."));

            if (removeBackFaceTriangles.boolValue)
            {
                GUIDraw.PropertyField(backFaceTriangleMode, new GUIContent("Backface Mode", "Use 'Box' if your camera always stays within a box volume.\n\nUse 'Direction' if you camera always faces the same direction, doesn't rotate and moves in the same direction."), true);
                    
                if (backFaceTriangleMode.enumValueIndex == (int)MeshCombiner.BackFaceTriangleMode.Direction)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PrefixLabel("Direction");
                    EditorGUI.indentLevel--;

                    EditorGUILayout.PropertyField(backFaceDirection, GUIContent.none);
                    if (GUILayout.Button("R", EditorStyles.miniButtonMid, GUILayout.Width(25))) backFaceDirection.vector3Value = Vector3.zero;
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    DrawBounds(backFaceBounds);
                }

                GUIDraw.PropertyField(twoSidedShadows, new GUIContent("Two Sided Shadows", "When using shadows these need to be two sided after removing backfases from a mesh."), true);
            }

            if (weldVertices.boolValue)
            {
                EditorGUILayout.HelpBox("Only use this option if you want to use the mesh for shadows only.", MessageType.Warning, true);
            } 

            GUIDraw.PropertyField(weldVertices, new GUIContent("Weld Vertices", "This will combine all vertices that share the same position"));
            if (weldVertices.boolValue)
            {
                GUIDraw.PropertyField(weldSnapVertices, new GUIContent("Snap Vertices", ""), true);
                if (weldSnapVertices.boolValue)
                {
                    GUIDraw.PropertyField(weldSnapSize, new GUIContent("Snap Size", ""), true);
                    if (weldSnapSize.floatValue < 0.00001f) weldSnapSize.floatValue = 0.00001f;
                }
                GUIDraw.PropertyField(weldIncludeNormals, new GUIContent("Include Normals", ""), true);
            }
            
            if (addMeshColliders.boolValue)
            {
                EditorGUILayout.HelpBox("Only use this option if you do not have primitive colliders on the original GameObjects or if you remove geometry, otherwise it will slow down physics.", MessageType.Warning, true);
            }

            GUIDraw.PropertyField(addMeshColliders, new GUIContent("Add Mesh Colliders", "Add mesh colliders to the combined meshes. Only use this option if you do not have primative colliders on the original GameObjects."));

            if (addMeshColliders.boolValue)
            {
                GUIDraw.PropertyField(addMeshCollidersInRange, new GUIContent("Use Range", "Add mesh colliders only that are within range"), true);
                if (addMeshCollidersInRange.boolValue) DrawBounds(addMeshCollidersBounds);
            }

            string meshesUnreadableTooltip = "Only disable this option if you want to read from the meshes at runtime. Otherwise making meshes unreadable removes the mesh copy from CPU memory, which saves memory.";

            if (!makeMeshesUnreadable.boolValue)
            {
                EditorGUILayout.HelpBox(meshesUnreadableTooltip, MessageType.Warning, true);
            }
            GUIDraw.PropertyField(makeMeshesUnreadable, new GUIContent("Make Meshes Unreadable", meshesUnreadableTooltip));

            EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Use Vertex Output Limit", "Combined meshes won't exceed this vertex count."));
                EditorGUILayout.PropertyField(useVertexOutputLimit, GUIContent.none, GUILayout.Width(25));
                if (useVertexOutputLimit.boolValue)
                {
                    EditorGUILayout.PropertyField(vertexOutputLimit, GUIContent.none);
                }
            
                if (vertexOutputLimit.intValue < 1) vertexOutputLimit.intValue = 1;

            EditorGUILayout.EndHorizontal();

            if (Lightmapping.bakedGI && !Lightmapping.realtimeGI)
            {
                if (copyBakedLighting.boolValue)
                {
                    EditorGUILayout.HelpBox("Copy baked lighting will results in more combined meshes (more draw calls than with rebaking) as the source objects need to have the same lightmap index. The advantage is that the Scene file size doesn't increase when used with 'Combine In Runtime'.", MessageType.Info, true);
                }
                GUIDraw.PropertyField(copyBakedLighting, new GUIContent("Copy Baked Lighting", "The Lighting of the original meshes will be copied to the combined meshes."));
                if (copyBakedLighting.boolValue) rebakeLighting.boolValue = false;
                validCopyBakedLighting.boolValue = copyBakedLighting.boolValue;
            }
            if (!combineInRuntime.boolValue && !Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Rebake Lighting", "Rebake the lighting on the combines meshes."));
                EditorGUILayout.PropertyField(rebakeLighting, GUIContent.none, GUILayout.Width(25));
                if (rebakeLighting.boolValue)
                {
                    copyBakedLighting.boolValue = false;
                    EditorGUILayout.PropertyField(rebakeLightingMode, GUIContent.none);
                }
                EditorGUILayout.EndHorizontal();

                if (rebakeLighting.boolValue)
                {
                    GUIDraw.PropertyField(scaleInLightmap, new GUIContent("Scale In Lightmap", "The scale of the combined meshes in the Lightmap, default value is 1 and a smaller value will create Lightmaps with less file size."), true);
                    if (scaleInLightmap.floatValue < 0.0f) scaleInLightmap.floatValue = 0.0f;
                }
            }

            //EditorGUILayout.PropertyField(removeGeometryBelowTerrain);
            //if (removeGeometryBelowTerrain.boolValue)
            //{
            //    DrawPropertyArray(terrains);
            //}

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        void DrawBounds(SerializedProperty boundsProp)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.indentLevel++;
            EditorGUILayout.PrefixLabel("Center");
            EditorGUI.indentLevel--;

            Bounds bounds = boundsProp.boundsValue;
            bounds.center = EditorGUILayout.Vector3Field(GUIContent.none, bounds.center);
            if (GUILayout.Button("R", EditorStyles.miniButtonMid, GUILayout.Width(25))) bounds.center = Vector3.zero;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.indentLevel++;
            EditorGUILayout.PrefixLabel("Size");
            EditorGUI.indentLevel--;
            bounds.size = EditorGUILayout.Vector3Field(GUIContent.none, bounds.size);
            bounds.size = Mathw.SetMin(bounds.size, 0.001f);
            if (GUILayout.Button("R", EditorStyles.miniButtonMid, GUILayout.Width(25))) bounds.size = Vector3.one;
            boundsProp.boundsValue = bounds;
            EditorGUILayout.EndHorizontal();
        }

        void DrawJobSettings()
        {
            Color color;
            if (meshCombiner.meshCombineJobs.Count > 0)
            {
                color = Color.Lerp(Color.yellow, Color.green, Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 3)));
                repaint = true;
            }
            else color = Color.yellow;

            MeshCombineJobManager meshCombineJobManager = MeshCombineJobManager.instance;

            GUIDraw.DrawHeader(jobSettingsFoldout, new GUIContent("Job Settings", ""), color * editorSkinMulti);

            if (!jobSettingsFoldout.boolValue) { EditorGUILayout.EndVertical(); return; }

            EditorGUI.indentLevel++;

            GUI.changed = false;
            GUIDraw.PropertyField(combineJobMode, new GUIContent("Combine Job Mode", "Should meshes be combined all at once or per frame."));
            if ((MeshCombineJobManager.CombineJobMode)combineJobMode.enumValueIndex == MeshCombineJobManager.CombineJobMode.CombinePerFrame)
            {
                GUIDraw.PropertyField(combineMeshesPerFrame, new GUIContent("Meshes Per Frame"), true);
                if (combineMeshesPerFrame.intValue < 1) combineMeshesPerFrame.intValue = 1;
                else if (combineMeshesPerFrame.intValue > 128) combineMeshesPerFrame.intValue = 128;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Use Multi Threading", "Combine meshes using multi threading which results in higher fps and faster combining."));
            EditorGUILayout.PropertyField(useMultiThreading, GUIContent.none, GUILayout.Width(25));
            if (useMultiThreading.boolValue)
            {
                EditorGUILayout.PropertyField(threadAmountMode, GUIContent.none);
            }
            EditorGUILayout.EndHorizontal();

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && meshCombiner.combineInRuntime)
            {
                EditorGUILayout.HelpBox("WebGL doesn't support the use of multi threading.", MessageType.Info, true);
                useMultiThreading.boolValue = false;
            }

            if (useMultiThreading.boolValue)
            {
                if ((MeshCombineJobManager.ThreadAmountMode)threadAmountMode.enumValueIndex == MeshCombineJobManager.ThreadAmountMode.Custom)
                {
                    GUIDraw.PropertyField(customThreadAmount, new GUIContent("Custom Thread Amount"), true);
                    if (customThreadAmount.intValue < 1) customThreadAmount.intValue = 1;
                    else if (customThreadAmount.intValue > meshCombineJobManager.cores) customThreadAmount.intValue = meshCombineJobManager.cores;
                }
                GUIDraw.PropertyField(useMainThread, new GUIContent("Use Main Thread"), true);
            }

            if (meshCombiner != null && meshCombiner.meshCombineJobs.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Jobs Pending");
                    EditorGUILayout.LabelField("");
                    Rect rect = GUILayoutUtility.GetLastRect();
                    rect.x += 16; rect.width -= 16;
                    EditorGUI.ProgressBar(rect, (float)(meshCombiner.totalMeshCombineJobs - meshCombiner.meshCombineJobs.Count) / meshCombiner.totalMeshCombineJobs, meshCombiner.meshCombineJobs.Count.ToString());
                EditorGUILayout.EndHorizontal();
            }

            bool guiChanged = GUI.changed;

            GUIDraw.PropertyField(showStats, new GUIContent("Show Stats"));

            if (showStats.boolValue)
            {
                GUIDraw.DrawSpacer();

                LabelField("Meshes Cached", meshCombineJobManager.meshCacheDictionary.Count.ToString());
                LabelField("Meshes Arrays Cached", meshCombineJobManager.newMeshObjectsPool.Count.ToString());
                LabelField("Total New Mesh Objects", meshCombineJobManager.totalNewMeshObjects.ToString());
                LabelField("Mesh Combine Jobs", meshCombineJobManager.meshCombineJobs.Count.ToString());

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Mesh Combine Jobs Thread");
                for (int i = 0; i < meshCombineJobManager.cores; i++)
                {
                    var meshCombineJobThread = meshCombineJobManager.meshCombineJobsThreads[i];
                    if (meshCombineJobThread.threadState == MeshCombineJobManager.ThreadState.isFree) GUI.color = Color.blue;
                    else if (meshCombineJobThread.threadState == MeshCombineJobManager.ThreadState.isReady) GUI.color = Color.green;
                    else if (meshCombineJobThread.threadState == MeshCombineJobManager.ThreadState.isRunning) GUI.color = Color.yellow;
                    else if (meshCombineJobThread.threadState == MeshCombineJobManager.ThreadState.hasError) GUI.color = Color.red;
                    EditorGUILayout.LabelField(meshCombineJobManager.meshCombineJobsThreads[i].meshCombineJobs.Count.ToString() + " ", GUILayout.Width(35));
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                // EditorGUILayout.BeginHorizontal();
                // EditorGUILayout.PrefixLabel("New Mesh Objects Jobs?");
                // EditorGUILayout.LabelField(meshCombineJobManager.newMeshObjectsJobs.Count.ToString());
                // EditorGUILayout.EndHorizontal();

                //EditorGUILayout.BeginHorizontal();
                //EditorGUILayout.PrefixLabel("New Mesh Objects Done Thread");
                //EditorGUILayout.LabelField(meshCombineJobManager.newMeshObjectsDoneThread.Count.ToString());
                //EditorGUILayout.EndHorizontal();

                //EditorGUILayout.BeginHorizontal();
                //EditorGUILayout.PrefixLabel("New Mesh Objects Done");
                //EditorGUILayout.LabelField(meshCombineJobManager.newMeshObjectsDone.Count.ToString());
                //EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            
            if (guiChanged)
            {
                serializedObject.ApplyModifiedProperties();
                meshCombiner.CopyJobSettingsToAllInstances();
                meshCombineJobManager.SetJobMode(meshCombiner.jobSettings);
            }
        }

        void LabelField(string prefix, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(prefix);
            EditorGUILayout.LabelField(value);
            EditorGUILayout.EndHorizontal();
        }

        void DrawRuntime(Color color)
        {
            GUIDraw.DrawHeader(runtimeSettingsFoldout, new GUIContent("Runtime Settings", ""), color * editorSkinMulti);

            if (!runtimeSettingsFoldout.boolValue) { EditorGUILayout.EndVertical(); return; }

            EditorGUI.indentLevel++;

            GUI.changed = false;
            GUIDraw.PropertyField(combineInRuntime, new GUIContent("Combine In Runtime", "Combine meshes at runtime."));

            if (combineInRuntime.boolValue)
            {
                if (GUI.changed)
                {
                    meshCombiner.RestoreOriginalRenderersAndLODGroups();
                    activeOriginal.boolValue = true;
                }

                GUIDraw.PropertyField(combineOnStart, new GUIContent("Combine On Start", "Combine meshes on start up."));

                GUIDraw.PropertyField(originalMeshRenderers, new GUIContent("Original Mesh Renderers", "What to do with the origal MeshRenderer components."));
                GUIDraw.PropertyField(originalLODGroups, new GUIContent("Original LODGroups", "What to do with the original LODGroup components."));

                if ((MeshCombiner.HandleComponent)originalMeshRenderers.enumValueIndex == MeshCombiner.HandleComponent.Disable && (MeshCombiner.HandleComponent)originalLODGroups.enumValueIndex == MeshCombiner.HandleComponent.Disable)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(new GUIContent("On/Off MCS With 'Tab'", "Toggle beween MCS combined meshes rendering and original GameObjects rendering."));
                    EditorGUILayout.PropertyField(useCombineSwapKey, GUIContent.none, GUILayout.Width(25));
                    //if (useCombineSwapKey.boolValue)
                    //{
                    //    EditorGUILayout.PropertyField(combineSwapKey, GUIContent.none);
                    //}
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        void DrawCombining()
        {
            if (meshCombiner.searchOptions.useSearchBox)
            {
                if (!meshCombiner.combined && (searchConditions.searchBoxPivot.vector3Value != meshCombiner.oldPosition || searchConditions.searchBoxSize.vector3Value != meshCombiner.oldScale))
                {
                    if (meshCombiner.octreeContainsObjects)
                    {
                        // Debug.Log("Reset");
                        meshCombiner.ResetOctree();
                    }
                    meshCombiner.oldPosition = searchConditions.searchBoxPivot.vector3Value;
                    meshCombiner.oldScale = searchConditions.searchBoxSize.vector3Value;
                }
            }

            if (searchConditions.searchBoxSquare.boolValue)
            {
                float sizeX = searchConditions.searchBoxSize.vector3Value.x;
                searchConditions.searchBoxSize.vector3Value = new Vector3(sizeX, sizeX, sizeX);
            }

            EditorGUILayout.BeginVertical("box");

            float buttonWidth = (EditorGUIUtility.currentViewWidth * 0.5f) - 10;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Search", GUILayout.Width(buttonWidth)))
            {
                meshCombiner.AddObjectsAutomatically();
                SceneView.RepaintAll();
            }

            if (!combineInRuntime.boolValue || !combineOnStart.boolValue)
            {
                GUILayout.Space(10);

                string buttonText;
                if (meshCombiner.meshCombineJobs.Count == 0) buttonText = "Combine"; else buttonText = "Cancel";
                if (GUILayout.Button(buttonText))
                {
                    if (meshCombiner.meshCombineJobs.Count == 0) meshCombiner.CombineAll();
                    else
                    {
                        meshCombiner.AbortAndClearMeshCombineJobs();
                    }
                }
            }
            // GUILayout.Space(3);
            EditorGUILayout.EndHorizontal();

            if (meshCombiner.selectImportSettingsMeshes.Count > 0)
            {
                GUIDraw.DrawSpacer();
                float v = Mathf.Abs(Mathf.Cos(Time.realtimeSinceStartup * 5));
                GUI.backgroundColor = new Color(v, 0, 0, 1);
                if (GUILayout.Button("Select Meshes for Import Settings"))
                {
                    SelectMeshesmportSettings();
                }
                GUI.backgroundColor = Color.white;

                repaint = true;
            }

            if (meshCombiner.unreadableMeshes.Count > 0)
            {
                GUIDraw.DrawSpacer();
                float v = Mathf.Abs(Mathf.Cos(Time.realtimeSinceStartup * 5));
                GUI.backgroundColor = new Color(v, 0, 0, 1);
                if (GUILayout.Button("Make Meshes Readable"))
                {
                    MakeMeshesReadableInImportSettings();
                    meshCombiner.AddObjectsAutomatically();
                }
                GUI.backgroundColor = Color.white;

                repaint = true;
            }

            bool hasFoundObjects = (meshCombiner.data.foundObjects.Count > 0 || meshCombiner.data.foundLodObjects.Count > 0);
            bool hasCombinedChildren = (meshCombiner.transform.childCount > 0) || (meshCombiner.combineMode == CombineMode.DynamicObjects && meshCombiner.data.combinedGameObjects.Count > 0);

            GUIDraw.DrawSpacer(2.5f, 5, 2.5f);

            EditorGUILayout.BeginHorizontal();
            if (!hasFoundObjects) GUI.color = Color.grey;
            if (GUILayout.Button("Select Original", GUILayout.Width(buttonWidth)))
            {
                if (hasFoundObjects) Methods.SelectChildrenWithMeshRenderer(meshCombiner.searchOptions.parentGOs);
            }
            if (!hasCombinedChildren) GUI.color = Color.grey; else GUI.color = Color.white;
            GUILayout.Space(10);
            if (GUILayout.Button("Select Combined"))
            {
                if (hasCombinedChildren) Methods.SelectChildrenWithMeshRenderer(meshCombiner.transform);
            }
            GUI.color = Color.white;
            // GUILayout.Space(3);
            EditorGUILayout.EndHorizontal();

            if (hasFoundObjects)
            {
                GUIDraw.DrawSpacer(2.5f, 5, 2.5f);
                string buttonText;
                if (activeOriginal.boolValue) { buttonText = "Disable Original Renderers and LODGroups"; GUI.backgroundColor = new Color(0.70f, 0.70f, 1); }
                else { buttonText = "Enable Original Renderers and LODGroups"; GUI.backgroundColor = new Color(0.70f, 1, 0.70f); }

                if (GUILayout.Button(buttonText))
                {
                    activeOriginal.boolValue = !activeOriginal.boolValue;
                    foreach (var mcsCombiner in targets) ((MeshCombiner)mcsCombiner).ExecuteHandleObjects(activeOriginal.boolValue, MeshCombiner.HandleComponent.Disable, MeshCombiner.HandleComponent.Disable);
                }
                GUI.backgroundColor = Color.white;
                GUIDraw.DrawSpacer(2.5f, 5, 2.5f);
                //GUILayout.Space(5);

            }

            if (hasCombinedChildren)
            {
                EditorGUILayout.BeginHorizontal();

                // GUILayout.Space(3);

                if (GUILayout.Button(new GUIContent("Save Combined", "Only use this if you want the combined meshes not to be saved with your Scene or if you want to reuse them in another Scene."), GUILayout.Width(buttonWidth)))
                {
                    if (Event.current.shift) meshCombiner.saveMeshesFolder = Application.dataPath;
                    SaveCombinedMeshes();
                }

                GUILayout.Space(10);

                GUI.backgroundColor = new Color(0.5f, 0.25f, 0.25f, 1);
                if (GUILayout.Button("Delete Combined"))
                {
                    meshCombiner.Reset();
                }
                GUI.backgroundColor = Color.white;
                // GUILayout.Space(3);
                EditorGUILayout.EndHorizontal();
                GUIDraw.DrawSpacer(2.5f, 5, 5);
            }

            BeginVertical(Color.white * editorSkinMulti);
            GUIDraw.LabelWidthUnderline(new GUIContent("Gizmos"), 14);
            GUIDraw.PropertyField(drawGizmos, new GUIContent("Draw Gizmos"));
            if (drawGizmos.boolValue)
            {
                GUIDraw.PropertyField(drawMeshBounds, new GUIContent("Draw Mesh Bounds"));
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);

            DisplayOctreeInfo(); 

            if (DisplayVertsAndTrisInfo()) GUIDraw.DrawSpacer();

            EditorGUILayout.EndVertical();
            
            // Debug.Log("Time " + stopwatch.ElapsedMilliseconds);
        }

        void DisplayOctreeInfo()
        {
            // if (!meshCombiner.octreeContainsObjects) return;
            BeginVertical(Color.magenta * editorSkinMulti);
            
            FoundCombineConditions foundCombineConditions = meshCombiner.foundCombineConditions;

            GUIDraw.LabelWidthUnderline(new GUIContent("Found Objects " + (meshCombiner.data.foundObjects.Count + meshCombiner.data.foundLodGroups.Count), ""), 14);
            GUILayout.Space(-4);
            GUIDraw.PrefixAndLabel(new GUIContent("Material"), new GUIContent(foundCombineConditions.matCount.ToString()));
            if (foundCombineConditions.lightmapIndexCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Lightmap Index"), new GUIContent(foundCombineConditions.lightmapIndexCount.ToString()));
            if (foundCombineConditions.shadowCastingCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Cast Shadows"), new GUIContent(foundCombineConditions.shadowCastingCount.ToString()));
            if (foundCombineConditions.receiveShadowsCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Receive Shadows"), new GUIContent(foundCombineConditions.receiveShadowsCount.ToString()));
            if (foundCombineConditions.receiveGICount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Receive GI"), new GUIContent(foundCombineConditions.receiveGICount.ToString()));
            if (foundCombineConditions.lightProbeUsageCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Light Probes"), new GUIContent(foundCombineConditions.lightProbeUsageCount.ToString()));
            if (foundCombineConditions.reflectionProbeUsageCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Reflection Probes"), new GUIContent(foundCombineConditions.reflectionProbeUsageCount.ToString()));
            if (foundCombineConditions.probeAnchorCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Anchor Override"), new GUIContent(foundCombineConditions.probeAnchorCount.ToString()));
            if (foundCombineConditions.motionVectorGenerationModeCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Motion Vectors"), new GUIContent(foundCombineConditions.motionVectorGenerationModeCount.ToString()));
            if (foundCombineConditions.staticEditorFlagsCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Static Flags"), new GUIContent(foundCombineConditions.staticEditorFlagsCount.ToString()));
            if (foundCombineConditions.layerCount > 1) GUIDraw.PrefixAndLabel(new GUIContent("Layer"), new GUIContent(foundCombineConditions.layerCount.ToString()));
            GUIDraw.DrawUnderLine(2);
            GUIDraw.PrefixAndLabel(new GUIContent("Combine Conditions"), new GUIContent(foundCombineConditions.combineConditionsCount.ToString()));
            GUIDraw.DrawUnderLine(2);
            GUIDraw.PrefixAndLabel(new GUIContent("Cells"), new GUIContent(meshCombiner.cellCount.ToString()));
            GUIDraw.DrawUnderLine(2);
            GUILayout.Space(5); 
            MeshCombiner.LodParentHolder[] lodParentsCount = meshCombiner.lodParentHolders;
             
            if (lodParentsCount == null || lodParentsCount.Length == 0 || meshCombiner.data.foundLodGroups.Count == 0)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            GUIDraw.LabelWidthUnderline(new GUIContent("Found LOD Groups", ""), 14);
             
            for (int i = 0; i < lodParentsCount.Length; i++)
            {
                MeshCombiner.LodParentHolder lodParentCount = lodParentsCount[i];
                if (!lodParentCount.found) continue;

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("LOD Group " + (i + 1) + " |", GUILayout.Width(95));

                int[] lods = lodParentCount.lods;

                for (int j = 0; j < lods.Length; j++)
                {
                    if (lods[j] == lods[0]) GUI.color = Color.green; else GUI.color = Color.red;
                    // EditorGUILayout.LabelField("LOD" + j + " -> " + lods[j] + " Objects");
                    EditorGUILayout.LabelField(lods[j].ToString(), GUILayout.Width(38));
                    GUI.color = Color.white;
                    EditorGUILayout.LabelField("|", GUILayout.Width(7));
                }
                EditorGUILayout.EndHorizontal();
                GUIDraw.DrawUnderLine();
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        void Label(string text1, string text2, string text3, string text4, float width1, float width2)
        {
            GUILayout.Label(GUIContent.none);
            Rect rect = GUILayoutUtility.GetLastRect();
            float windowWidth = rect.width + 15;
            rect.width = width1;
            GUI.Label(rect, text1);
            rect.x += width1;
            rect.width = width2;
            GUI.Label(rect, text2);
            rect.x += width2;
            GUI.Label(rect, text3);
            rect.x += width2;
            if (rect.x + rect.width > windowWidth) rect.width = windowWidth - rect.x;
            GUI.Label(rect, text4);
        }

        void BeginVertical(Color color)
        {
            GUI.color = color;
            GUILayout.BeginVertical("Box");
            GUI.color = Color.white;
        }

        bool DisplayVertsAndTrisInfo()
        {
            if (!meshCombiner.combined) return false;

            // GUIDraw.LabelWidthUnderline(new GUIContent("Combine Stats"), 14);

            float width1 = 70; 
            float width2 = 80;
            
            BeginVertical(Color.white * editorSkinMulti);
            Label("", "Batches", "Vertices", "Triangles", width1, width2);
            EditorGUILayout.EndVertical();

            BeginVertical(Color.blue * editorSkinMulti);

            Label("Original", meshCombiner.originalDrawCalls.ToString(), meshCombiner.originalTotalVertices.ToString(), meshCombiner.originalTotalTriangles.ToString(), width1, width2);
            EditorGUILayout.EndVertical();

            BeginVertical(Color.green * editorSkinMulti);

            Label("Combined", meshCombiner.newDrawCalls.ToString(), meshCombiner.totalVertices.ToString(), meshCombiner.totalTriangles.ToString(), width1, width2);

            EditorGUILayout.EndVertical();

            BeginVertical(Color.yellow * editorSkinMulti);

            if (meshCombiner.originalTotalVertices != 0)
            {
               Label("Saved", (((meshCombiner.originalDrawCalls - meshCombiner.newDrawCalls) / (float)meshCombiner.originalDrawCalls) * 100).ToString("F1") + "%",
                     (((meshCombiner.originalTotalVertices - meshCombiner.totalVertices) / (float)meshCombiner.originalTotalVertices) * 100).ToString("F2") + "%",
                     (((meshCombiner.originalTotalTriangles - meshCombiner.totalTriangles) / (float)meshCombiner.originalTotalTriangles) * 100).ToString("F2") + "%", width1, width2);

                Label("Boost", (100 + (((meshCombiner.originalDrawCalls - meshCombiner.newDrawCalls) / (float)meshCombiner.newDrawCalls) * 100)).ToString("F1") + "%",
                      (100 + (((meshCombiner.originalTotalVertices - meshCombiner.totalVertices) / (float)meshCombiner.totalVertices) * 100)).ToString("F2") + "%",
                      (100 + (((meshCombiner.originalTotalTriangles - meshCombiner.totalTriangles) / (float)meshCombiner.totalTriangles) * 100)).ToString("F2") + "%", width1, width2);
            }
            EditorGUILayout.EndVertical();

            BeginVertical(Color.yellow * editorSkinMulti);

            GUILayout.Label("Combine Time: " + (meshCombiner.combineTime * 1000).ToString("F2") + " ms");

            EditorGUILayout.EndVertical();

            return true;
        }

        public void SelectMeshesmportSettings()
        {
            HashSet<Mesh> meshes = meshCombiner.selectImportSettingsMeshes;
            Mesh[] meshArray = new Mesh[meshes.Count];
            meshes.CopyTo(meshArray);

            Selection.objects = meshArray;

            meshes.Clear();
        }

        public void MakeMeshesReadableInImportSettings()
        {
            HashSet<Mesh> unreadableMeshes = meshCombiner.unreadableMeshes;

            foreach (Mesh mesh in unreadableMeshes)
            {
                string path = AssetDatabase.GetAssetPath(mesh);
                if (path.Length > 0)
                {
                    var modelImporter = (ModelImporter)ModelImporter.GetAtPath(path);
                    modelImporter.isReadable = true;
                    modelImporter.SaveAndReimport();
                    Debug.Log("(MeshCombineStudio) => Read/Write Enabled on " + path);
                }
            }
            unreadableMeshes.Clear();
        }

        void SaveCombinedMeshes()
        {
            if (meshCombiner.saveMeshesFolder == "") meshCombiner.saveMeshesFolder = Application.dataPath;

            string savePath = EditorUtility.SaveFolderPanel("Save Combined Meshes", meshCombiner.saveMeshesFolder, "");
            if (savePath == "") return;
            else if (!savePath.Contains(Application.dataPath))
            {
                Debug.Log("(MeshCombineStudio) => Meshes need to be saved in one of this project folders.");
                return;
            }

            meshCombiner.saveMeshesFolder = savePath;

            MeshFilter[] mfs = meshCombiner.transform.GetComponentsInChildren<MeshFilter>();

            if (mfs == null || mfs.Length == 0)
            {
                Debug.Log("(MeshCombineStudio) => No meshes are found for saving");
                return;
            }

            // Debug.Log(savePath);

            try
            {
                string assetPath = savePath.Replace(Application.dataPath, "Assets");

                for (int i = 0; i < mfs.Length; i++)
                {
                    Mesh mesh = mfs[i].sharedMesh;
                    if (mesh == null)
                    {
                        Debug.LogError("(MeshCombineStudio) => " + mfs[i].name + " mesh is null!");
                        continue;
                    }

                    if (AssetDatabase.Contains(mesh))
                    {
                        Debug.LogError("(MeshCombineStudio) => " + mesh.name + " is already saved. You first need to recombine before you can overwrite the meshes.");
                        return;
                    }

                    string fileName = mesh.name + "_" + i.ToString() + ".asset"; ;
                    string filePath = assetPath + "/" + fileName;
                    if (EditorUtility.DisplayCancelableProgressBar("Saving meshes to disk", filePath + " (" + i + "/" + mfs.Length + ")", (float)i / mfs.Length))
                    {
                        break;
                    }

                    GarbageCollectMesh garbageCollectMesh = mfs[i].GetComponent<GarbageCollectMesh>();
                    if (garbageCollectMesh != null)
                    {
                        garbageCollectMesh.mesh = null;
                        DestroyImmediate(garbageCollectMesh);
                    }

                    // Debug.Log(filePath);
                    // Debug.Log(savePath + "/" + fileName);
                    if (File.Exists(savePath + "/" + fileName)) AssetDatabase.DeleteAsset(filePath);
                    AssetDatabase.CreateAsset(mesh, filePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        void GetBatching(out bool staticBatching, out bool dynamicBatching)
        {
            MethodInfo getBatchingForPlatForm = typeof(PlayerSettings).GetMethod("GetBatchingForPlatform", BindingFlags.Static | BindingFlags.NonPublic);

            object tempStatic = null, tempDynamic = null;
            object[] args = new object[] { EditorUserBuildSettings.activeBuildTarget, tempStatic, tempDynamic };
            getBatchingForPlatForm.Invoke(typeof(PlayerSettings), args);
            staticBatching = ((int)args[1]) == 1 ? true : false;
            dynamicBatching = ((int)args[2]) == 1 ? true : false;
        }

        void SetBatchingActive(bool staticActive, bool dynamicActive)
        {
            MethodInfo setBatchingForPlatForm = typeof(PlayerSettings).GetMethod("SetBatchingForPlatform", BindingFlags.Static | BindingFlags.NonPublic);
            object[] args2 = new object[] { EditorUserBuildSettings.activeBuildTarget, staticActive ? 1 : 0, dynamicActive ? 1 : 0 };
            setBatchingForPlatForm.Invoke(typeof(PlayerSettings), args2);
        }

        void ApplyScaleLimit()
        {
            Vector3 size = searchConditions.searchBoxSize.vector3Value;
            Vector3 newSize = size;

            if (newSize.x < 0.01f) newSize.x = 0.01f;
            if (newSize.y < 0.01f) newSize.y = 0.01f;
            if (newSize.z < 0.01f) newSize.z = 0.01f;

            if (newSize != size) searchConditions.searchBoxSize.vector3Value = newSize;
        }

        void ApplyTransformLock()
        {
            meshCombiner = (MeshCombiner)target;
            Transform t = meshCombiner.transform;
            if (t.childCount == 0) meshCombiner.combined = false;

            t.position = Vector3.zero;
            t.localScale = Vector3.one;
        }
    }
}