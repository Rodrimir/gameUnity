using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameYunit.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameYunit.KitBridge
{
    /// <summary>Ensaio opt-in com processos reais; só executa com --coop-role e --coop-smoke.</summary>
    public sealed class CoopSmokeProbe : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public string role, protocol, stage, scene, timestamp;
            public bool success;
            public float localDistance, remoteDistance, jumpHeight;
            public bool remoteWalkAnimation, remoteJumpAnimation, localCamera, remoteInputBlocked, menuDoesNotPause;
            public string localName, remoteName;
            public List<string> checks = new();
            public List<string> errors = new();
        }

        private Evidence _evidence;
        private string _resultPath;
        private CoopSession _session;
        private bool _active;
        private string[] _args;

        private string Arg(string name, string fallback = "")
        {
            int index = Array.IndexOf(_args, name);
            return index >= 0 && index + 1 < _args.Length ? _args[index + 1] : fallback;
        }

        private IEnumerator Start()
        {
            _args = Environment.GetCommandLineArgs();
            string role = Arg("--coop-role");
            if (string.IsNullOrEmpty(role)) yield break;
            yield return null;
            _session = GetComponent<CoopSession>();
            if (CoopSession.Instance != _session) yield break;
            bool smoke = _args.Contains("--coop-smoke");
            string protocol = Arg("--coop-protocol", "udp");
            _evidence = new Evidence { role = role, protocol = protocol, stage = "starting" };
            _resultPath = Arg("--coop-evidence", $"Logs/Network/{protocol}-{role}.json");
            if (smoke)
            {
                string root = Path.GetFullPath(".") + Path.DirectorySeparatorChar;
                _resultPath = Path.GetFullPath(_resultPath);
                if (!_resultPath.StartsWith(root, StringComparison.Ordinal)) throw new ArgumentException("A evidência precisa ficar dentro do projeto.");
                Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));
                _active = true;
                Application.logMessageReceived += CaptureError;
            }
            _session.StartSession(role == "host", Arg("--coop-address", "127.0.0.1"), Arg("--coop-port", "7777"),
                Arg("--coop-name", role == "host" ? "Host Azul" : "Cliente Laranja"), role == "host" ? 0 : 1,
                protocol == "ws" ? ConnectionProtocol.WebSocket : ConnectionProtocol.Udp);
            if (!smoke) yield break;
            Write("waiting-for-two");
            yield return Run();
        }

        private IEnumerator Run()
        {
            float deadline = Time.realtimeSinceStartup + 150;
            CoopPlayer local = null, remote = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                var players = FindObjectsByType<CoopPlayer>(FindObjectsSortMode.None).Where(p => p.IsSpawned).ToArray();
                local = players.FirstOrDefault(p => p.IsOwner);
                remote = players.FirstOrDefault(p => !p.IsOwner);
                if (players.Length == 2 && local != null && remote != null) break;
                yield return null;
            }
            if (local == null || remote == null) { Fail("Dois jogadores não nasceram dentro do prazo."); yield break; }
            Check(SceneManager.GetActiveScene().name == CoopSession.LevelScene, "Level1_Coop carregada pela rede");
            _evidence.localName = local.GetComponent<NetworkPlayerIdentity>().PlayerName.Value.ToString();
            _evidence.remoteName = remote.GetComponent<NetworkPlayerIdentity>().PlayerName.Value.ToString();
            Check(_evidence.localName != _evidence.remoteName && _evidence.remoteName.Length > 0, "Nomes distintos replicados");
            Check(local.GetComponent<NetworkPlayerIdentity>().ColorIndex.Value != remote.GetComponent<NetworkPlayerIdentity>().ColorIndex.Value, "Cores replicadas");
            _evidence.localCamera = local.LocalCamera != null && local.LocalCamera.follow == local.transform;
            _evidence.remoteInputBlocked = !remote.Input.enabled && !remote.GetComponent<CharacterController>().enabled;
            Check(_evidence.localCamera, "Câmera segue apenas o dono");
            Check(_evidence.remoteInputBlocked && local.Input.enabled, "Input e CharacterController exclusivos do dono");
            Vector3 initial = local.transform.position, remoteInitial = remote.transform.position;
            var remoteAnimator = remote.GetComponent<Animator>();
            local.Input.UseTestInput = true;
            float started = Time.realtimeSinceStartup;
            Write("exercising");
            while (Time.realtimeSinceStartup - started < 9)
            {
                if (local == null || remote == null) { Fail("Jogador desapareceu durante o ensaio."); yield break; }
                float elapsed = Time.realtimeSinceStartup - started;
                local.Input.TestMovement = elapsed > 1 && elapsed < 2.6f ? Vector2.up : elapsed > 2.6f && elapsed < 4 ? Vector2.down : Vector2.zero;
                local.Input.TestJump = elapsed > 4 && elapsed < 4.5f;
                bool menu = _session.Manager.IsHost && elapsed > 6 && elapsed < 6.8f;
                if (_session.LocalMenuOpen != menu) _session.SetLocalMenu(menu);
                _evidence.localDistance = Mathf.Max(_evidence.localDistance, Vector3.ProjectOnPlane(local.transform.position - initial, Vector3.up).magnitude);
                _evidence.remoteDistance = Mathf.Max(_evidence.remoteDistance, Vector3.ProjectOnPlane(remote.transform.position - remoteInitial, Vector3.up).magnitude);
                _evidence.jumpHeight = Mathf.Max(_evidence.jumpHeight, local.transform.position.y - initial.y);
                _evidence.remoteWalkAnimation |= remoteAnimator.GetFloat("ForwardSpeed") > 1;
                _evidence.remoteJumpAnimation |= !remoteAnimator.GetBool("Grounded") && remoteAnimator.GetFloat("AirborneVerticalSpeed") > 1;
                if (menu && Mathf.Approximately(Time.timeScale, 1)) _evidence.menuDoesNotPause = true;
                yield return null;
            }
            local.Input.TestMovement = Vector2.zero;
            local.Input.TestJump = false;
            local.Input.UseTestInput = false;
            Check(_evidence.localDistance > 1, "Movimento local com input pelo controlador do kit");
            Check(_evidence.remoteDistance > 1, "Transform do parceiro recebido pela rede");
            Check(_evidence.jumpHeight > 0.5f, "Salto local com CharacterController");
            Check(_evidence.remoteWalkAnimation && _evidence.remoteJumpAnimation, "Animações de corrida e salto recebidas pelo NetworkAnimator");
            if (_session.Manager.IsHost) Check(_evidence.menuDoesNotPause, "Menu Esc local mantém Time.timeScale em 1");
            Write("disconnecting");
            if (!_session.Manager.IsHost)
            {
                _session.Leave();
                deadline = Time.realtimeSinceStartup + 30;
                while ((_session.Busy || SceneManager.GetActiveScene().name != CoopSession.MenuScene) && Time.realtimeSinceStartup < deadline) yield return null;
                Check(!_session.Busy && SceneManager.GetActiveScene().name == CoopSession.MenuScene, "Cliente voltou ao menu após sair");
                Check(FindObjectsByType<CoopPlayer>(FindObjectsSortMode.None).Length == 0, "Jogadores e controles removidos ao desconectar");
                Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Cursor liberado após desconexão");
            }
            else
            {
                deadline = Time.realtimeSinceStartup + 35;
                while (_session.Manager.ConnectedClientsIds.Count > 1 && Time.realtimeSinceStartup < deadline) yield return null;
                Check(_session.Manager.IsHost && _session.Manager.ConnectedClientsIds.Count == 1, "Host permanece aberto após saída do cliente");
                Check(FindObjectsByType<CoopPlayer>(FindObjectsSortMode.None).Length == 1, "Réplica do cliente removida no Host");
                _session.SetLocalMenu(true);
            }
            _evidence.success = _evidence.errors.Count == 0;
            _active = false;
            Application.logMessageReceived -= CaptureError;
            Write(_evidence.success ? "passed" : "failed");
            if (_evidence.role == "client") Application.Quit(_evidence.success ? 0 : 1);
        }

        private void Check(bool condition, string message)
        {
            if (condition) _evidence.checks.Add(message);
            else _evidence.errors.Add(message);
        }

        private void Fail(string message)
        {
            _evidence.errors.Add(message);
            _active = false;
            Write("failed");
            if (_evidence.role == "client") Application.Quit(1);
        }

        private void CaptureError(string message, string stack, LogType type)
        {
            if (_active && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) _evidence.errors.Add(message);
        }

        private void Write(string stage)
        {
            _evidence.stage = stage;
            _evidence.scene = SceneManager.GetActiveScene().name;
            _evidence.timestamp = DateTime.UtcNow.ToString("O");
            File.WriteAllText(_resultPath, JsonUtility.ToJson(_evidence, true));
            Debug.Log($"[CoopSmokeProbe] {stage}: {_resultPath}");
        }

        private void OnDestroy() => Application.logMessageReceived -= CaptureError;
    }
}
