using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool interact;
		public bool flashLight;
		public bool crouch;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
			else
			{
				LookInput(Vector2.zero);
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		public void OnInteract(InputValue value)
		{
			InteractInput(value.isPressed);
		}

		public void OnFlashLight(InputValue value)
		{
			FlashLightInput(value.isPressed);
		}

		public void OnChrouch(InputValue value)
		{
			if (value.isPressed)
			{
				crouch = true;
			}
		}

		public void OnCrouch(InputValue value)
		{
			if (value.isPressed)
			{
				crouch = true;
			}
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		public void InteractInput(bool newInteractState)
		{
			interact = newInteractState;
		}

		public void FlashLightInput(bool newFlashLightState)
		{
			flashLight = newFlashLightState;
		}

		public void CrouchInput(bool newCrouchState)
		{
			if (newCrouchState)
			{
				crouch = true;
			}
		}

		/// <summary>
		/// Clears all input axes and state flags. Useful during cutscenes and transitions.
		/// </summary>
		public void ResetInputs()
		{
			move = Vector2.zero;
			look = Vector2.zero;
			jump = false;
			sprint = false;
			interact = false;
			crouch = false;
		}
		
		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}