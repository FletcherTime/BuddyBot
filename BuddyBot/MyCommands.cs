using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.VoiceNext;
using DSharpPlus.EventArgs;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace BuddyBot
{
    public class MyCommands
    {
        // Used for separating users' voice streams
        private ConcurrentDictionary<uint, Process> ffmpegs;

        [Command("join")]
        public async Task Join(CommandContext ctx)
        {
            // Fetches the VoiceNext client
            var vnext = ctx.Client.GetVoiceNextClient();

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc != null)
            {
                throw new InvalidOperationException("Already connected in this server.");
            }

            // Checks if the calling user is in a voice channel
            var chn = ctx.Member?.VoiceState?.Channel;
            if (chn == null)
            {
                throw new InvalidOperationException("You must be connected to a voice channel.");
            }

            // Connects to the user's server
            vnc = await vnext.ConnectAsync(chn);

            // Place bot voice response here

            this.ffmpegs = new ConcurrentDictionary<uint, Process>();
            vnc.VoiceReceived += OnVoiceReceived;
        }

        [Command("leave")]
        public async Task Leave(CommandContext ctx)
        {
            // Fetches the VoiceNext client
            var vnext = ctx.Client.GetVoiceNextClient();

            // Checks if currently connected to a channel
            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                throw new InvalidOperationException("Not already connected in this server.");
            }

            // Clears the dictionary and voice streams
            vnc.VoiceReceived -= OnVoiceReceived;
            foreach (var kvp in this.ffmpegs)
            {
                await kvp.Value.StandardInput.BaseStream.FlushAsync();
                kvp.Value.StandardInput.Dispose();
                kvp.Value.WaitForExit();
            }
            this.ffmpegs = null;

            // Place bot voice response here

            vnc.Disconnect();
        }

        public async Task OnVoiceReceived(VoiceReceiveEventArgs ea)
        {
            if (!this.ffmpegs.ContainsKey(ea.SSRC))
            {
                // Starts writing audio to file using ffmpeg
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $@"-ac 2 -f s16le -ar 48000 -i pipe:0 -ac 2 -ar 44100 {ea.SSRC}.wav",
                    RedirectStandardInput = true
                };

                // Adds voice stream to dictionary
                this.ffmpegs.TryAdd(ea.SSRC, Process.Start(psi));
            }

            var buff = ea.Voice.ToArray();

            var ffmpeg = this.ffmpegs[ea.SSRC];
            await ffmpeg.StandardInput.BaseStream.WriteAsync(buff, 0, buff.Length);
            await ffmpeg.StandardInput.BaseStream.FlushAsync();
        }
    }
}
