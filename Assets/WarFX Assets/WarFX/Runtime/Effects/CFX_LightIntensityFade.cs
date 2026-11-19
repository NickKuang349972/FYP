using UnityEngine;
using System.Collections;

// Cartoon FX  - (c) 2015, Jean Moreno

// Decreases a light's intensity over time.

namespace WarFX
{
	[RequireComponent(typeof(Light))]
	public class CFX_LightIntensityFade : MonoBehaviour
	{
		// Duration of the effect.
		public float duration = 1.0f;

		// Delay of the effect.
		public float delay = 0.0f;

		/// Final intensity of the light.
		public float finalIntensity = 0.0f;

		// If <c>true</c>, light will destructs itself on completion of the effect
		public bool autodestruct;

		// Base intensity, automatically taken from light parameters.
		private float baseIntensity;

		private float p_lifetime = 0.0f;
		private float p_delay;
		private bool finished;

		private Light cachedLight;

		void Awake()
		{
			cachedLight = GetComponent<Light>();
			// Captured before OnEnable can disable the light, so pooled re-activations keep the authored value.
			baseIntensity = cachedLight.intensity;
		}

		void OnEnable()
		{
			p_lifetime = 0.0f;
			p_delay = delay;
			finished = false;
			if(delay > 0f) cachedLight.enabled = false;
		}

		void Update()
		{
			if(finished) return;

			if(p_delay > 0f)
			{
				p_delay -= Time.deltaTime;
				if(p_delay <= 0f)
				{
					cachedLight.enabled = true;
				}
				return;
			}

			if(duration <= 0f)
			{
				cachedLight.intensity = finalIntensity;
				Finish();
				return;
			}

			if(p_lifetime / duration < 1.0f)
			{
				cachedLight.intensity = Mathf.Lerp(baseIntensity, finalIntensity, p_lifetime / duration);
				p_lifetime += Time.deltaTime;
			}
			else
			{
				Finish();
			}
		}

		private void Finish()
		{
			// Flag instead of disabling the component: pooled objects must be able to fade again when re-activated.
			finished = true;
			if(autodestruct)
				GameObject.Destroy(this.gameObject);
		}
	}
}
