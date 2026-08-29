using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.AI;
using TrustNoOne.AI;

namespace V0.Editor
{
    /// <summary>
    /// 1-Click Setup Tool for Enemy 2 (The Deceiver NPC).
    /// Accessible via Unity Menu: Tools > Setup Enemy 2 (Deceiver NPC)
    /// </summary>
    public static class Enemy2Setup
    {
        [MenuItem("Tools/Setup Enemy 2 (Deceiver NPC)", false, 30)]
        public static void SetupEnemy2()
        {
            // 1. Locate Enemy 2 in Scene
            GameObject enemy2Obj = GameObject.Find("Enemy2") ?? GameObject.Find("enemy2");

            if (enemy2Obj == null)
            {
                // Find any GameObject with Enemy2 in name
                GameObject[] allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (GameObject go in allGos)
                {
                    if (go.name.ToLower().Contains("enemy2") || go.name.ToLower().Contains("deceiver"))
                    {
                        enemy2Obj = go;
                        break;
                    }
                }
            }

            if (enemy2Obj == null)
            {
                EditorUtility.DisplayDialog("Enemy 2 Not Found", "Could not find 'Enemy2' GameObject in the active scene! Please drag Enemy2 into the scene first.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(enemy2Obj, "Setup Enemy 2");

            // 2. Setup NavMeshAgent
            NavMeshAgent agent = enemy2Obj.GetComponent<NavMeshAgent>();
            if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(enemy2Obj);
            agent.speed = 2.0f;
            agent.stoppingDistance = 2.5f;
            agent.acceleration = 14f;
            agent.angularSpeed = 240f;
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.autoBraking = true;

            // 3. Setup Collider
            CapsuleCollider col = enemy2Obj.GetComponent<CapsuleCollider>();
            if (col == null) col = Undo.AddComponent<CapsuleCollider>(enemy2Obj);
            col.isTrigger = true;
            col.radius = 0.35f;
            col.height = 1.8f;
            col.center = new Vector3(0, 0.9f, 0);

            // 4. Setup Animator Controller parameters & transitions
            Animator animator = enemy2Obj.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false; // CRITICAL: NavMeshAgent controls position
                if (animator.runtimeAnimatorController != null)
                {
                    SetupAnimatorController(animator.runtimeAnimatorController as AnimatorController);
                }
            }

            // 5. Setup DeceiverAI component
            DeceiverAI deceiverAI = enemy2Obj.GetComponent<DeceiverAI>();
            if (deceiverAI == null) deceiverAI = Undo.AddComponent<DeceiverAI>(enemy2Obj);

            // Auto-assign player reference
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                SerializedObject so = new SerializedObject(deceiverAI);
                so.FindProperty("_player").objectReferenceValue = playerObj.transform;
                if (animator != null)
                {
                    so.FindProperty("_animator").objectReferenceValue = animator;
                    so.FindProperty("_modelTransform").objectReferenceValue = animator.transform;
                }
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(enemy2Obj);
            Selection.activeGameObject = enemy2Obj;
            EditorGUIUtility.PingObject(enemy2Obj);

            EditorUtility.DisplayDialog(
                "Enemy 2 Setup Complete!",
                "Enemy 2 (Deceiver NPC) has been successfully configured!\n\n" +
                "✓ NavMeshAgent configured (speed: 2.0, stopping dist: 2.5m)\n" +
                "✓ Animator Controller parameters ('Speed', 'OpenDoor') & transitions created\n" +
                "✓ DeceiverAI component attached\n" +
                "✓ Auto-follows player & opens closed doors\n\n" +
                "You can now press Play to test Enemy 2!",
                "OK"
            );

            Debug.Log("<color=green><b>[Enemy2Setup]</b> Enemy 2 (Deceiver NPC) successfully set up!</color>");
        }

        private static void SetupAnimatorController(AnimatorController controller)
        {
            if (controller == null) return;

            // 1. Add Parameters if missing
            bool hasSpeed = false;
            bool hasOpenDoor = false;

            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (p.name == "Speed") hasSpeed = true;
                if (p.name == "OpenDoor") hasOpenDoor = true;
            }

            if (!hasSpeed)
            {
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            }
            if (!hasOpenDoor)
            {
                controller.AddParameter("OpenDoor", AnimatorControllerParameterType.Trigger);
            }

            // 2. Find States
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = null;
            AnimatorState walkingState = null;
            AnimatorState doorOpeningState = null;

            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                string stateName = childState.state.name.ToLower();
                if (stateName.Contains("idle")) idleState = childState.state;
                else if (stateName.Contains("walk")) walkingState = childState.state;
                else if (stateName.Contains("door")) doorOpeningState = childState.state;
            }

            if (idleState == null || walkingState == null) return;

            // Make sure Idle is default state
            stateMachine.defaultState = idleState;

            // 3. Clear existing transitions to rebuild clean transitions
            idleState.transitions = new AnimatorStateTransition[0];
            walkingState.transitions = new AnimatorStateTransition[0];
            if (doorOpeningState != null) doorOpeningState.transitions = new AnimatorStateTransition[0];

            // 4. Idle -> Walking (Speed > 0.1)
            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkingState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.2f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // 5. Walking -> Idle (Speed < 0.1)
            AnimatorStateTransition walkToIdle = walkingState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.2f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // 6. Door Opening Transitions (Explicit one-shot transitions to prevent looping)
            if (doorOpeningState != null)
            {
                // Idle -> DoorOpening
                AnimatorStateTransition idleToDoor = idleState.AddTransition(doorOpeningState);
                idleToDoor.hasExitTime = false;
                idleToDoor.duration = 0.1f;
                idleToDoor.AddCondition(AnimatorConditionMode.If, 0, "OpenDoor");

                // Walking -> DoorOpening
                AnimatorStateTransition walkToDoor = walkingState.AddTransition(doorOpeningState);
                walkToDoor.hasExitTime = false;
                walkToDoor.duration = 0.1f;
                walkToDoor.AddCondition(AnimatorConditionMode.If, 0, "OpenDoor");

                // DoorOpening -> Idle (Exit Time = 0.9)
                AnimatorStateTransition doorToIdle = doorOpeningState.AddTransition(idleState);
                doorToIdle.hasExitTime = true;
                doorToIdle.exitTime = 0.92f;
                doorToIdle.duration = 0.2f;

                // DoorOpening -> Walking (Exit Time = 0.9, Speed > 0.1)
                AnimatorStateTransition doorToWalk = doorOpeningState.AddTransition(walkingState);
                doorToWalk.hasExitTime = true;
                doorToWalk.exitTime = 0.92f;
                doorToWalk.duration = 0.2f;
                doorToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>✓ [Enemy2Setup]</color> Configured Animator Controller transitions and parameters!");
        }
    }
}
