using UnityEngine;
using System.Collections;

namespace WarFX
{
	/// <summary>
	/// Destroys (or deactivates) this GameObject once its ParticleSystem has finished playing.
	/// Attached to most effects of the pack; pooled instances are switched to OnlyDeactivate by CFX_SpawnSystem.
	/// </summary>
	[RequireComponent(typeof(ParticleSystem))]
	public class CFX_AutoDestructShuriken : MonoBehaviour
	{
		public bool OnlyDeactivate;

		// Shared instance: avoids allocating a WaitForSeconds on every poll of every effect.
		static private readonly WaitForSeconds checkInterval = new WaitForSeconds(0.5f);

		private ParticleSystem ps;

		void Awake()
		{
			ps = GetComponent<ParticleSystem>();
		}

		void OnEnable()
		{
			// Pooled objects are re-activated many times: drop any leftover poller before starting a new one.
			StopAllCoroutines();
			StartCoroutine(CheckIfAlive());
		}

		IEnumerator CheckIfAlive()
		{
			while(true)
			{
				yield return checkInterval;

				if(!ps.IsAlive(true))
				{
					if(OnlyDeactivate)
					{
						this.gameObject.SetActive(false);
					}
					else
					{
						GameObject.Destroy(this.gameObject);
					}
					break;
				}
			}
		}
	}
}
