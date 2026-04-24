document.addEventListener("DOMContentLoaded", function () {
    const container = document.getElementById('ultimate-effect-container');
    const activeMode = '@hieuung';

    if (activeMode === 'none' || !activeMode) return;
    const modesConfig = {
        emojiBase: { quantity: 80, minSize: 15, maxSize: 25, minSpeed: 7, maxSpeed: 15 },
        sakura: { icons: ['🌸', '🌺'], useBase: 'emojiBase' },
        autumn: { icons: ['🍁', '🍂'], useBase: 'emojiBase' },
        snow: { icons: ['❄️', '❅', '❆'], useBase: 'emojiBase', styleOverride: { color: 'white', textShadow: '0 0 5px rgba(255,255,255,0.8)' } },
        grades: { icons: ['A', 'B+', 'A', 'B+'], useBase: 'emojiBase', isGrades: true, styleOverride: { color: '#e74a3b', fontWeight: '900', fontFamily: 'Arial, sans-serif' } },
        rain: { quantity: 60, minSpeed: 0.8, maxSpeed: 1.5 }
    };
    function initEffect() {
        container.innerHTML = '';
        if (activeMode === 'fireworks') {
            initCanvasFireworksHighEnd();
        }
        else if (modesConfig[activeMode]?.useBase === 'emojiBase') { initFallingEmojis(activeMode); }
        else if (activeMode === 'rain') { initRain(); }
    }

    // ===================================
    //  CANVAS FIREWORKS HIGH-END ENGINE 
    // ===================================
    function initCanvasFireworksHighEnd() {
        const canvas = document.createElement('canvas');
        container.appendChild(canvas);
        const ctx = canvas.getContext('2d');
        let cw = window.innerWidth;
        let ch = window.innerHeight;
        canvas.width = cw; canvas.height = ch;

        window.addEventListener('resize', () => { cw = window.innerWidth; ch = window.innerHeight; canvas.width = cw; canvas.height = ch; });

        const particles = [];
        let lastLaunchTime = 0;
        const FW_CONFIG = {
            frequency: 1200,
            particleCount: 180,
            explodePower: 15,
            friction: 0.96,
            gravity: 0.08,
            trailLength: 0.15,
            colors: ['#ff0040', '#00ffff', '#ffff00', '#ff8000', '#00ff80', '#ffffff', '#da00ff']
        };

        function random(min, max) { return Math.random() * (max - min) + min; }
        class Particle {
            constructor(x, y, hue) {
                this.x = x; this.y = y;
                this.coordinates = [];
                this.coordinateCount = 6;
                while (this.coordinateCount--) { this.coordinates.push([this.x, this.y]); }
                this.angle = random(0, Math.PI * 2);
                this.speed = random(1, FW_CONFIG.explodePower);
                this.vx = Math.cos(this.angle) * this.speed;
                this.vy = Math.sin(this.angle) * this.speed;
                this.hue = hue;
                this.brightness = random(60, 90);
                this.alpha = 1;
                this.decay = random(0.008, 0.015);
            }

            update(index) {
                this.coordinates.pop();
                this.coordinates.unshift([this.x, this.y]);
                this.vx *= FW_CONFIG.friction;
                this.vy *= FW_CONFIG.friction;
                this.vy += FW_CONFIG.gravity;
                this.x += this.vx;
                this.y += this.vy;
                this.alpha -= this.decay;
                if (this.alpha <= 0) { particles.splice(index, 1); }
            }

            draw() {
                ctx.beginPath();
                ctx.moveTo(this.coordinates[this.coordinates.length - 1][0], this.coordinates[this.coordinates.length - 1][1]);
                ctx.lineTo(this.x, this.y);
                ctx.strokeStyle = `hsla(${this.hue}, 100%, ${this.brightness}%, ${this.alpha})`;
                ctx.lineWidth = 2;
                ctx.lineCap = 'round';
                ctx.stroke();
            }
        }

        function explode(x, y) {
            let count = FW_CONFIG.particleCount;
            const baseColorHex = FW_CONFIG.colors[Math.floor(random(0, FW_CONFIG.colors.length))];
            let baseHue = 0;
            if (baseColorHex === '#ff0040') baseHue = 345;
            else if (baseColorHex === '#00ffff') baseHue = 180;
            else if (baseColorHex === '#ffff00') baseHue = 60;
            else if (baseColorHex === '#da00ff') baseHue = 280;
            else baseHue = random(0, 360);

            while (count--) {
                particles.push(new Particle(x, y, random(baseHue - 20, baseHue + 20)));
            }
        }
        function animate(currentTime) {
            requestAnimationFrame(animate);
            ctx.globalCompositeOperation = 'destination-out';
            ctx.fillStyle = `rgba(0, 0, 0, ${FW_CONFIG.trailLength})`;
            ctx.fillRect(0, 0, cw, ch);
            ctx.globalCompositeOperation = 'lighter';
            let i = particles.length;
            while (i--) {
                particles[i].draw();
                particles[i].update(i);
            }
            if (currentTime - lastLaunchTime > FW_CONFIG.frequency) {
                explode(random(cw * 0.1, cw * 0.9), random(ch * 0.1, ch * 0.8));
                lastLaunchTime = currentTime;
            }
        }
        animate();
    }
    function initFallingEmojis(modeKey) {
        const config = { ...modesConfig.emojiBase, ...modesConfig[modeKey] }; const iconList = config.icons;
        for (let i = 0; i < config.quantity; i++) {
            const item = document.createElement('div'); item.classList.add('falling-emoji');
            item.textContent = iconList[Math.floor(Math.random() * iconList.length)];
            if (config.styleOverride) Object.assign(item.style, config.styleOverride);
            item.style.left = Math.random() * 100 + 'vw'; item.style.fontSize = Math.random() * (config.maxSize - config.minSize) + config.minSize + 'px';
            item.style.opacity = (Math.random() * 0.4 + 0.6).toFixed(2); item.style.animationDuration = Math.random() * (config.maxSpeed - config.minSpeed) + config.minSpeed + 's';
            container.appendChild(item);
        }
    }
    function initRain() {
        const config = modesConfig.rain;
        for (let i = 0; i < config.quantity; i++) {
            const drop = document.createElement('div'); drop.classList.add('rain-drop');
            drop.style.left = Math.random() * 100 + 'vw'; drop.style.height = (Math.random() * 10 + 20) + 'px';
            drop.style.animationDuration = Math.random() * (config.maxSpeed - config.minSpeed) + config.minSpeed + 's';
            container.appendChild(drop);
        }
    }

    initEffect();
});