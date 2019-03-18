﻿#if!ODIN_INSPECTOR


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pixeye
{
	[CustomEditor(typeof(Object), true, isFallback = true)]
	[CanEditMultipleObjects]
	public class EditorOverride : Editor
	{
		Dictionary<string, Cache> cache = new Dictionary<string, Cache>();
		List<SerializedProperty> props = new List<SerializedProperty>();
		SerializedProperty propScript;
		Type type;
		int length;
		List<FieldInfo> objectFields;

		bool initialized;

		//	Colors colors;
		FoldoutGroupAttribute prevFold;
		GUIStyle style;


		void OnEnable()
		{
			bool pro = EditorGUIUtility.isProSkin;
//			if (!pro)
//			{
//				colors = new Colors();
//				colors.col0 = new Color(0.2f, 0.2f, 0.2f, 1f);
//				colors.col1 = new Color(1, 1, 1, 0.55f);
//				colors.col2 = new Color(0.7f, 0.7f, 0.7f, 1f);
//			}
//			else
//			{
//				colors = new Colors();
//				colors.col0 = new Color(0.2f, 0.2f, 0.2f, 1f);
//				colors.col1 = new Color(1, 1, 1, 0.1f);
//				colors.col2 = new Color(0.25f, 0.25f, 0.25f, 1f);
//			}

			var t        = target.GetType();
			var typeTree = t.GetTypeTree();
			objectFields = target.GetType()
					.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
					           BindingFlags.Instance)
					.OrderByDescending(x => typeTree.IndexOf(x.DeclaringType))
					.ToList();


			length = objectFields.Count;


			Repaint();
			initialized = false;
		}

		private void OnDisable()
		{
			foreach (var cach in cache)
			{
				cach.Value.Dispose();
			}
		}


		public override void OnInspectorGUI()
		{
			serializedObject.Update();


			if (!initialized)
			{
				var uiTex_in    = Resources.Load<Texture2D>("IN foldout focus-6510");
				var uiTex_in_on = Resources.Load<Texture2D>("IN foldout focus on-5718");


				var c_on = Color.white;

				style = new GUIStyle(EditorStyles.foldout);

				style.overflow = new RectOffset(-10, 0, 3, 0);
				style.padding = new RectOffset(25, 0, -3, 0);

				style.active.textColor = c_on;
				style.active.background = uiTex_in;
				style.onActive.textColor = c_on;
				style.onActive.background = uiTex_in_on;

				style.focused.textColor = c_on;
				style.focused.background = uiTex_in;
				style.onFocused.textColor = c_on;
				style.onFocused.background = uiTex_in_on;


				for (var i = 0; i < length; i++)
				{
					var fold = Attribute.GetCustomAttribute(objectFields[i], typeof(FoldoutGroupAttribute)) as FoldoutGroupAttribute;

					Cache c;
					if (fold == null)
					{
						if (prevFold != null && prevFold.foldEverything)
						{
							if (!cache.TryGetValue(prevFold.name, out c))
							{
								cache.Add(prevFold.name, new Cache {atr = prevFold, types = new HashSet<string> {objectFields[i].Name}});
							}
							else
							{
								c.types.Add(objectFields[i].Name);
							}
						}

						continue;
					}

					prevFold = fold;
					if (!cache.TryGetValue(fold.name, out c))
					{
						cache.Add(fold.name, new Cache {atr = fold, types = new HashSet<string> {objectFields[i].Name}});
					}
					else
					{
						c.types.Add(objectFields[i].Name);
					}
				}


				var property = serializedObject.GetIterator();
				var next     = property.NextVisible(true);
				if (next)
				{
					do
					{
						HandleProp(property);
					} while (property.NextVisible(false));
				}
			}


			if (props.Count == 0)
			{
				DrawDefaultInspector();
				return;
			}

			initialized = true;

			using (new EditorGUI.DisabledScope("m_Script" == props[0].propertyPath))
			{
				EditorGUILayout.PropertyField(props[0], true);
			}

			foreach (var pair in cache)
			{
				this.UseVerticalBoxLayout(() => {
					pair.Value.expanded = EditorGUILayout.Foldout(pair.Value.expanded, pair.Value.atr.name, true,
							style != null ? style : EditorStyles.foldout);

					if (pair.Value.expanded)
					{
						EditorGUI.indentLevel = 1;
						for (int i = 0; i < pair.Value.props.Count; i++)
						{
							this.UseVerticalBoxLayout(() => {
								EditorGUILayout.PropertyField(pair.Value.props[i],
										new GUIContent(pair.Value.props[i].name.FirstLetterToUpperCase()), true);

								//if (i == pair.Value.props.Count - 1)
								//EditorGUILayout.Space();
							}, EditorUIStyles.boxChild);
						}
					}
				}, EditorUIStyles.box);
				EditorGUI.indentLevel = 0;
			}


			for (var i = 1; i < props.Count; i++)
			{
				EditorGUILayout.PropertyField(props[i], true);
			}


			serializedObject.ApplyModifiedProperties();
			EditorGUILayout.Space();
		}


		public void HandleProp(SerializedProperty prop)
		{
			bool shouldBeFolded = false;

			foreach (var pair in cache)
			{
				if (pair.Value.types.Contains(prop.name))
				{
					shouldBeFolded = true;
					pair.Value.props.Add(prop.Copy());

					break;
				}
			}

			if (shouldBeFolded == false)
			{
				props.Add(prop.Copy());
			}
		}


		class Cache
		{
			public HashSet<string> types = new HashSet<string>();
			public List<SerializedProperty> props = new List<SerializedProperty>();
			public FoldoutGroupAttribute atr;
			public bool expanded;

			public void Dispose()
			{
				props.Clear();
				types.Clear();
				atr = null;
			}
		}
	}


	public static class FrameworkExtensions
	{
		public static string FirstLetterToUpperCase(this string s)
		{
			if (string.IsNullOrEmpty(s))
				return string.Empty;

			char[] a = s.ToCharArray();
			a[0] = char.ToUpper(a[0]);
			return new string(a);
		}

		public static IList<Type> GetTypeTree(this Type t)
		{
			var types = new List<Type>();
			while (t.BaseType != null)
			{
				types.Add(t);
				t = t.BaseType;
			}

			return types;
		}
	}
}
#endif