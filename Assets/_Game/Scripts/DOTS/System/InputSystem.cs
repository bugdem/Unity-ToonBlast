using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameEngine.Core
{
	[UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
	public partial class InputSystem : SystemBase
	{
		private TouchControls _touchControls;
		private Entity _inputEntity;
		private Camera _camera;

		protected override void OnCreate()
		{
			RequireForUpdate<InputTag>();

			_touchControls = new TouchControls();
		}

		protected override void OnStartRunning()
		{
			_touchControls.Enable();
			_touchControls.Player.Touch.performed += OnTouch;

			_inputEntity = SystemAPI.GetSingletonEntity<InputTag>();

			_camera = Camera.main;
		}

		protected override void OnUpdate()
		{
			var touchPosition = _touchControls.Player.TouchPosition.ReadValue<Vector2>();

			SystemAPI.SetSingleton(new InputTouchPosition { Value = touchPosition });
		}

		protected override void OnStopRunning()
		{
			_touchControls.Player.Touch.performed -= OnTouch;
			_touchControls.Disable();

			_inputEntity = Entity.Null;
		}

		private void OnTouch(InputAction.CallbackContext obj)
		{
			if (!SystemAPI.Exists(_inputEntity)) return;

			float2 screenPosition = _touchControls.Player.TouchPosition.ReadValue<Vector2>();
			SystemAPI.SetComponent<InputTouch>(_inputEntity, new InputTouch { 
				Value = screenPosition,
				Ray = _camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f))
			});
			SystemAPI.SetComponentEnabled<InputTouch>(_inputEntity, true);
		}
	}
}