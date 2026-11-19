using UnityEngine;
using System.Collections;

/**
 *	Handles the bullet hole decals:
 *	- Sets a random frame
 *	- Fades the material according to the defined lifetime
 *	- Optionally rotates the decal
 *	
 *	(c) 2015, Jean Moreno
**/

namespace WarFX
{
	[RequireComponent(typeof(MeshFilter))]
	public class WFX_BulletHoleDecal : MonoBehaviour
	{
		static private Vector2[] quadUVs = new Vector2[]{new Vector2(0,0), new Vector2(0,1), new Vector2(1,0), new Vector2(1,1)};

		// Property IDs resolved once, instead of hashing the shader property name on every frame.
		static private readonly int tintColorId = Shader.PropertyToID("_TintColor");

		public float lifetime = 10f;
		public float fadeoutpercent = 80;
		public Vector2 frames;
		public bool randomRotation = false;
		public bool deactivate = false;

		private float life;
		private float fadeout;
		private Color color;
		private float orgAlpha;

		private Renderer cachedRenderer;
		private Mesh cachedMesh;
		private MaterialPropertyBlock propertyBlock;

		void Awake()
		{
			EnsureInitialized();
		}

		// Awake is NOT called again after a domain reload during Play (scripts recompile,
		// etc.), which resets non-serialized runtime fields to null while OnEnable still
		// fires. Re-resolve everything lazily so pooled decals survive that.
		private bool tintInitialized;

		private void EnsureInitialized()
		{
			if(cachedRenderer == null)
				cachedRenderer = GetComponent<Renderer>();
			if(propertyBlock == null)
				propertyBlock = new MaterialPropertyBlock();
			if(cachedMesh == null)
			{
				MeshFilter meshFilter = GetComponent<MeshFilter>();
				if(meshFilter != null)
					cachedMesh = meshFilter.mesh;
			}
			if(!tintInitialized)
			{
				tintInitialized = true;
				// Read the tint from the shared material: Renderer.material clones a material per decal
				// and never releases it, which leaks memory under sustained fire.
				Material sharedMaterial = cachedRenderer != null ? cachedRenderer.sharedMaterial : null;
				color = (sharedMaterial != null && sharedMaterial.HasProperty(tintColorId)) ? sharedMaterial.GetColor(tintColorId) : Color.white;
				orgAlpha = color.a;
			}
		}

		void OnEnable()
		{
			EnsureInitialized();

			//Random UVs
			if(frames.x >= 1f && frames.y >= 1f)
			{
				int random = Random.Range(0, (int)(frames.x*frames.y));
				int fx = (int)(random % frames.x);
				// Row index must be divided by the column count, otherwise non-square atlases pick the wrong frame.
				int fy = (int)(random / frames.x);

				//Set new UVs
				Vector2[] meshUvs = new Vector2[4];
				for(int i = 0; i < 4; i++)
				{
					meshUvs[i].x = (quadUVs[i].x + fx) * (1.0f/frames.x);
					meshUvs[i].y = (quadUVs[i].y + fy) * (1.0f/frames.y);
				}
				cachedMesh.uv = meshUvs;
			}

			//Random rotate
			if(randomRotation)
				this.transform.Rotate(0f,0f,Random.Range(0f,360f), Space.Self);

			//Start lifetime coroutine
			life = lifetime;
			fadeout = life * (fadeoutpercent/100f);
			color.a = orgAlpha;
			ApplyTint();

			StopAllCoroutines();
			StartCoroutine(holeUpdate());
		}

		IEnumerator holeUpdate()
		{
			while(life > 0f)
			{
				life -= Time.deltaTime;
				if(life <= fadeout)
				{
					color.a = fadeout > 0f ? Mathf.Lerp(0f, orgAlpha, Mathf.Clamp01(life/fadeout)) : 0f;
					ApplyTint();
				}

				yield return null;
			}

			if(deactivate)
				this.gameObject.SetActive(false);
		}

		private void ApplyTint()
		{
			if(cachedRenderer == null) return;

			EnsureInitialized();

			// Per-renderer override: leaves the shared material untouched, so no material instance is created.
			cachedRenderer.GetPropertyBlock(propertyBlock);
			propertyBlock.SetColor(tintColorId, color);
			cachedRenderer.SetPropertyBlock(propertyBlock);
		}
	}
}
