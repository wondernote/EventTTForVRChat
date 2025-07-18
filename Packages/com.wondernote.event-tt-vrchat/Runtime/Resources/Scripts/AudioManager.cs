
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WonderNote.EventTimeTable
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AudioManager : UdonSharpBehaviour
    {
        [SerializeField] private AudioSource hoverAudioSource;
        [SerializeField] private AudioSource clickAudioSource;
        [SerializeField] private AudioSource appearanceAudioSource;
        private VRCPlayerApi localPlayer;
        private bool isUserInVR = false;

        private void Start()
        {
            localPlayer = Networking.LocalPlayer;
            isUserInVR = localPlayer != null && localPlayer.IsUserInVR();

            if (isUserInVR) {
                hoverAudioSource.volume = 0.1f;
                clickAudioSource.volume = 0.6f;
                appearanceAudioSource.volume = 0.5f;
            }
        }

        public void PlayHoverSound()
        {
            hoverAudioSource.Play();

            if (isUserInVR) {
                localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.01f, 0.04f, 0.01f);
            }
        }

        public void PlayClickSound()
        {
            clickAudioSource.Play();
        }

        public void PlayAppearanceSound()
        {
            appearanceAudioSource.Play();
        }
    }
}
