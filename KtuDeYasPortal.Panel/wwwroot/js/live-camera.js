(() => {
    const peers = new Map();

    async function waitForIce(pc) {
        if (pc.iceGatheringState === "complete") return;
        await new Promise(resolve => {
            const timeout = setTimeout(resolve, 5000);
            pc.addEventListener("icegatheringstatechange", () => {
                if (pc.iceGatheringState === "complete") {
                    clearTimeout(timeout);
                    resolve();
                }
            });
        });
    }

    window.deyasLiveCamera = {
        formatExpiry(value) {
            return new Date(value).toLocaleString("tr-TR");
        },
        async start(videoId, whepUrl, token) {
            await this.stop(videoId);
            const video = document.getElementById(videoId);
            if (!video) throw new Error("Canlı görüntü alanı bulunamadı.");
            const pc = new RTCPeerConnection();
            peers.set(videoId, { pc, location: null, whepUrl, token });
            pc.addTransceiver("video", { direction: "recvonly" });
            pc.ontrack = event => {
                video.srcObject = event.streams[0];
                video.play().catch(() => {});
            };
            const offer = await pc.createOffer();
            await pc.setLocalDescription(offer);
            await waitForIce(pc);
            let response;
            for (let attempt = 0; attempt < 20; attempt++) {
                response = await fetch(whepUrl, {
                    method: "POST",
                    headers: { "Content-Type": "application/sdp", "Authorization": `Bearer ${token}` },
                    body: pc.localDescription.sdp
                });
                if (response.status !== 404) break;
                await new Promise(resolve => setTimeout(resolve, 1000));
            }
            if (!response.ok) {
                pc.close();
                peers.delete(videoId);
                throw new Error(response.status === 404
                    ? "20 saniye içinde kamera yayını ulaşmadığı için oturum kapatıldı. LattePanda kamera ve RTSP bağlantısını kontrol edip yeniden deneyin."
                    : `Canlı yayın bağlantısı kurulamadı (${response.status}).`);
            }
            const location = response.headers.get("Location");
            const answer = await response.text();
            await pc.setRemoteDescription({ type: "answer", sdp: answer });
            peers.set(videoId, { pc, location, whepUrl, token });
        },
        async stop(videoId) {
            const current = peers.get(videoId);
            if (!current) return;
            peers.delete(videoId);
            current.pc.close();
            const video = document.getElementById(videoId);
            if (video) video.srcObject = null;
            if (current.location) {
                const resource = new URL(current.location, current.whepUrl).toString();
                try {
                    await fetch(resource, {
                        method: "DELETE",
                        headers: { "Authorization": `Bearer ${current.token}` }
                    });
                } catch { /* WebRTC close still releases the browser resources. */ }
            }
        }
    };
})();
