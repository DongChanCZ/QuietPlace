using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
[RequireComponent(typeof(CharacterController))]
public sealed class RebuildPlayer:MonoBehaviour
{
 public float walkSpeed=3.6f,runSpeed=6f,lookSensitivity=.08f; public bool HasKeycard{get;private set;} public bool HasFlashlight{get;private set;}
 CharacterController controller;Camera view;Light flashlight;float pitch,fallSpeed;
 void Awake(){controller=GetComponent<CharacterController>();view=GetComponentInChildren<Camera>();flashlight=view.transform.Find("Player Flashlight").GetComponent<Light>();flashlight.enabled=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
 IRebuildInteractable Target(RaycastHit h)=>h.collider.GetComponentsInParent<MonoBehaviour>().OfType<IRebuildInteractable>().FirstOrDefault();
 void Update(){var kb=Keyboard.current;var mouse=Mouse.current;if(kb==null||mouse==null)return;if(kb.escapeKey.wasPressedThisFrame){bool u=Cursor.lockState==CursorLockMode.Locked;Cursor.lockState=u?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=u;}if(Cursor.lockState!=CursorLockMode.Locked)return;Vector2 look=mouse.delta.ReadValue()*lookSensitivity;transform.Rotate(0,look.x,0);pitch=Mathf.Clamp(pitch-look.y,-80,80);view.transform.localRotation=Quaternion.Euler(pitch,0,0);Vector2 input=new Vector2((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0),(kb.wKey.isPressed?1:0)-(kb.sKey.isPressed?1:0));Vector3 move=transform.right*input.x+transform.forward*input.y;if(move.sqrMagnitude>1)move.Normalize();fallSpeed=controller.isGrounded?-1.5f:fallSpeed+Physics.gravity.y*Time.deltaTime;controller.Move((move*(kb.leftShiftKey.isPressed?runSpeed:walkSpeed)+Vector3.up*fallSpeed)*Time.deltaTime);if(kb.eKey.wasPressedThisFrame&&Physics.SphereCast(view.transform.position,.08f,view.transform.forward,out RaycastHit hit,3f,~0,QueryTriggerInteraction.Collide))Target(hit)?.Interact(this);if(kb.fKey.wasPressedThisFrame&&HasFlashlight)flashlight.enabled=!flashlight.enabled;}
 public void GiveKeycard(){HasKeycard=true;UpdateObjective();}public void GiveFlashlight(){HasFlashlight=true;UpdateObjective();}
 void UpdateObjective(){if(HasKeycard&&HasFlashlight)RebuildGame.Instance.SetObjective("카드키로 경비실 슬라이드 도어를 여세요");else RebuildGame.Instance.SetObjective(HasKeycard?"책상에서 손전등을 챙기세요":"책상에서 카드키를 챙기세요");}
 void OnGUI(){GUI.Label(new Rect(18,16,700,25),"WASD 이동 / Shift 달리기 / E 상호작용 / F 손전등 / Esc 커서");GUI.Box(new Rect(18,48,470,30),"목표: "+RebuildGame.Instance.Objective);GUI.Label(new Rect(Screen.width/2-4,Screen.height/2-10,20,20),"+");if(Physics.SphereCast(view.transform.position,.08f,view.transform.forward,out RaycastHit hit,3f,~0,QueryTriggerInteraction.Collide)){var target=Target(hit);if(target!=null)GUI.Box(new Rect(Screen.width/2-120,Screen.height/2+28,240,30),"E  "+target.Prompt(this));}if(RebuildGame.Instance.ShowNotice)GUI.Box(new Rect(Screen.width/2-220,Screen.height-90,440,36),RebuildGame.Instance.Notice);}
}
