using UnityEngine;
using System.Collections;

/**
 *	Rapidly sets a light on/off.
 *	
 *	(c) 2015, Jean Moreno
**/

namespace WarFX
{
	[RequireComponent(typeof(Light))]
	public class WFX_LightFlicker : MonoBehaviour
	{
		public float time = 0.05f;

		private float timer;
		private Light cachedLight;

		void Awake()
		{
			cachedLight = GetComponent<Light>();
		}

		void OnEnable()
		{
			// Started from OnEnable (not Start) so pooled instances keep flickering when re-activated.
			timer = time;
			StopAllCoroutines();
			StartCoroutine(Flicker());
		}

		IEnumerator Flicker()
		{
			while(true)
			{
				cachedLight.enabled = !cachedLight.enabled;

				do
				{
					timer -= Time.deltaTime;
					yield return null;
				}
				while(timer > 0f);
				timer = time;
			}
		}
	}
}
