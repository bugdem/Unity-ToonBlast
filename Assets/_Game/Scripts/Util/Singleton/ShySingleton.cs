using UnityEngine;

namespace GameEngine.Util
{
	/// <summary>
	/// Singleton pattern. 
	///		Unlike Singleton, when accessed Instance, does not create a new gameobject if instance does not exists.
	///		This is useful to prevent errors when accessing Instance variable OnDestroy methods on other gameobjects.
	/// </summary>
	public class ShySingleton<T> : MonoBehaviour where T : Component
	{
		protected static T _instance;

		/// <summary>
		/// Singleton design pattern
		/// </summary>
		/// <value>The instance.</value>
		public static T Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = FindObjectOfType<T>();
				}
				return _instance;
			}
		}

		/// <summary>
		/// On awake, we initialize our instance. Make sure to call base.Awake() in override if you need awake.
		/// </summary>
		protected virtual void Awake()
		{
			if (!Application.isPlaying)
			{
				return;
			}

			_instance = this as T;
		}
	}
}