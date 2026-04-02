import * as THREE from './lib/threejs/three.module.min.js';
import { OrbitControls } from './lib/threejs/OrbitControls.js';

// 6-axis articulated robot welding simulation with spark particles
window.robotScene = {
    scene: null,
    camera: null,
    renderer: null,
    controls: null,
    joints: [],       // {group, axis, min, max, speed, phase}
    animationId: null,
    clock: null,
    tcp: null,         // tool center point marker
    trailPoints: [],
    trailLine: null,
    // Welding state
    sparks: [],        // {mesh, velocity, life, maxLife}
    sparkPool: [],     // reusable spark meshes
    weldLight: null,   // point light at weld point
    weldSeam: null,    // glowing weld seam line
    weldSeamPoints: [],
    isWelding: false,
    weldProgress: 0,   // 0..1 along the pipe
    axesHelpers: [],   // all AxesHelper instances for toggle
    axesVisible: true,

    init(canvasId) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return 'canvas not found';

        // Renderer
        const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
        renderer.setSize(canvas.clientWidth, canvas.clientHeight);
        renderer.setPixelRatio(window.devicePixelRatio);
        renderer.shadowMap.enabled = true;
        renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        renderer.setClearColor(0x1a1a2e);
        this.renderer = renderer;

        // Scene
        const scene = new THREE.Scene();
        scene.fog = new THREE.Fog(0x1a1a2e, 15, 30);
        this.scene = scene;

        // Camera
        const camera = new THREE.PerspectiveCamera(50, canvas.clientWidth / canvas.clientHeight, 0.1, 100);
        camera.position.set(2.0, 1.5, 2.0);
        camera.lookAt(0.5, 0.3, 0);
        this.camera = camera;

        // Orbit controls
        const controls = new OrbitControls(camera, canvas);
        controls.target.set(0.5, 0.3, 0);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.update();
        this.controls = controls;

        // Lights
        const ambientLight = new THREE.AmbientLight(0x404060, 0.6);
        scene.add(ambientLight);

        const dirLight = new THREE.DirectionalLight(0xffffff, 1.2);
        dirLight.position.set(5, 8, 5);
        dirLight.castShadow = true;
        dirLight.shadow.mapSize.width = 2048;
        dirLight.shadow.mapSize.height = 2048;
        dirLight.shadow.camera.near = 0.5;
        dirLight.shadow.camera.far = 25;
        dirLight.shadow.camera.left = -8;
        dirLight.shadow.camera.right = 8;
        dirLight.shadow.camera.top = 8;
        dirLight.shadow.camera.bottom = -8;
        scene.add(dirLight);

        const pointLight = new THREE.PointLight(0x4488ff, 0.5, 10);
        pointLight.position.set(-3, 4, -2);
        scene.add(pointLight);

        // Ground
        this._createGround(scene);

        // Grid
        const grid = new THREE.GridHelper(10, 20, 0x444466, 0x333355);
        grid.position.y = 0.001;
        scene.add(grid);

        // Robot
        this._createRobot(scene);

        // Coordinate axes at origin
        this._addAxes(scene, 0.5);

        // Weld light (hidden until welding starts)
        this.weldLight = new THREE.PointLight(0xffaa44, 0, 3);
        scene.add(this.weldLight);

        // Weld seam (thick bead filling the gap between plates)
        // Gap is 0.03 wide (Z), seam is slightly raised above plate surface
        const seamMat = new THREE.MeshStandardMaterial({
            color: 0xff6622, roughness: 0.5, metalness: 0.6,
            emissive: 0xff4400, emissiveIntensity: 0.3,
        });
        this._weldSeamMat = seamMat;
        this.weldSeam = null; // created dynamically during welding
        this.weldSeamPoints = [];

        // Pre-allocate spark pool
        this._initSparkPool(scene, 200);

        this.clock = new THREE.Clock();

        // Precompute weld path — solves IK using actual TCP world positions
        this._precomputeWeldPath();

        return 'initialized';
    },

    _createGround(scene) {
        const groundGeo = new THREE.PlaneGeometry(20, 20);
        const groundMat = new THREE.MeshStandardMaterial({
            color: 0x2a2a3e,
            roughness: 0.8,
            metalness: 0.2,
        });
        const ground = new THREE.Mesh(groundGeo, groundMat);
        ground.rotation.x = -Math.PI / 2;
        ground.receiveShadow = true;
        scene.add(ground);

        // Welding table — far enough from base (radius ~0.4) to avoid collision
        const tableGeo = new THREE.BoxGeometry(0.5, 0.4, 0.4);
        const tableMat = new THREE.MeshStandardMaterial({ color: 0x555577, roughness: 0.6, metalness: 0.4 });
        const table = new THREE.Mesh(tableGeo, tableMat);
        table.position.set(0.9, 0.2, 0);  // near edge at X=0.65, top at Y=0.4
        table.castShadow = true;
        table.receiveShadow = true;
        scene.add(table);

        // Workpiece: two steel plates on table with visible gap (butt joint)
        // Table top at Y=0.4. Plates are 0.04 thick → top at Y=0.44.
        const plateMat = new THREE.MeshStandardMaterial({ color: 0x888899, roughness: 0.3, metalness: 0.8 });

        // Front plate (positive Z side) — plates span X from 0.75 to 1.05
        const plate1Geo = new THREE.BoxGeometry(0.3, 0.04, 0.16);
        const plate1 = new THREE.Mesh(plate1Geo, plateMat);
        plate1.position.set(0.9, 0.42, 0.095);
        plate1.castShadow = true;
        plate1.receiveShadow = true;
        scene.add(plate1);

        // Back plate (negative Z side)
        const plate2Geo = new THREE.BoxGeometry(0.3, 0.04, 0.16);
        const plate2 = new THREE.Mesh(plate2Geo, plateMat.clone());
        plate2.position.set(0.9, 0.42, -0.095);
        plate2.castShadow = true;
        plate2.receiveShadow = true;
        scene.add(plate2);

        // Gap between plates: Z from -0.015 to +0.015 (3cm gap)
        // Plates span X from 0.75 to 1.05.
    },

    _createRobot(scene) {
        this.joints = [];
        const robotMat = new THREE.MeshStandardMaterial({ color: 0xff6600, roughness: 0.4, metalness: 0.6 });
        const jointMat = new THREE.MeshStandardMaterial({ color: 0x333344, roughness: 0.3, metalness: 0.7 });
        const darkMat = new THREE.MeshStandardMaterial({ color: 0x222233, roughness: 0.5, metalness: 0.5 });

        // Base plate
        const basePlateGeo = new THREE.CylinderGeometry(0.35, 0.4, 0.08, 32);
        const basePlate = new THREE.Mesh(basePlateGeo, darkMat);
        basePlate.position.y = 0.04;
        basePlate.castShadow = true;
        scene.add(basePlate);

        // J1: Base rotation (Y axis)
        const j1Group = new THREE.Group();
        j1Group.position.set(0, 0.08, 0);
        scene.add(j1Group);

        const baseGeo = new THREE.CylinderGeometry(0.25, 0.3, 0.3, 32);
        const base = new THREE.Mesh(baseGeo, robotMat);
        base.position.y = 0.15;
        base.castShadow = true;
        j1Group.add(base);

        this._addAxes(j1Group, 0.3);
        this.joints.push({ group: j1Group, axis: 'y', min: -Math.PI, max: Math.PI, speed: 0.3, phase: 0 });

        // J2: Shoulder (Z axis)
        const j2Group = new THREE.Group();
        j2Group.position.set(0, 0.4, 0);
        j1Group.add(j2Group);

        const shoulderGeo = new THREE.SphereGeometry(0.15, 16, 16);
        const shoulder = new THREE.Mesh(shoulderGeo, jointMat);
        shoulder.castShadow = true;
        j2Group.add(shoulder);

        this._addAxes(j2Group, 0.25);
        this.joints.push({ group: j2Group, axis: 'z', min: -Math.PI * 0.4, max: Math.PI * 0.3, speed: 0.25, phase: 0.5 });

        // Upper arm (0.5m)
        const upperArmGeo = new THREE.CylinderGeometry(0.07, 0.09, 0.5, 16);
        const upperArm = new THREE.Mesh(upperArmGeo, robotMat);
        upperArm.position.y = 0.25;
        upperArm.castShadow = true;
        j2Group.add(upperArm);

        // J3: Elbow (Z axis)
        const j3Group = new THREE.Group();
        j3Group.position.set(0, 0.5, 0);
        j2Group.add(j3Group);

        const elbowGeo = new THREE.SphereGeometry(0.12, 16, 16);
        const elbow = new THREE.Mesh(elbowGeo, jointMat);
        elbow.castShadow = true;
        j3Group.add(elbow);

        this._addAxes(j3Group, 0.2);
        this.joints.push({ group: j3Group, axis: 'z', min: -0.3, max: Math.PI * 0.7, speed: 0.35, phase: 1.0 });

        // Forearm (0.4m)
        const forearmGeo = new THREE.CylinderGeometry(0.05, 0.07, 0.4, 16);
        const forearm = new THREE.Mesh(forearmGeo, robotMat);
        forearm.position.y = 0.2;
        forearm.castShadow = true;
        j3Group.add(forearm);

        // J4: Wrist roll (Y axis)
        const j4Group = new THREE.Group();
        j4Group.position.set(0, 0.4, 0);
        j3Group.add(j4Group);

        const wristGeo = new THREE.CylinderGeometry(0.05, 0.05, 0.08, 16);
        const wrist = new THREE.Mesh(wristGeo, jointMat);
        wrist.castShadow = true;
        j4Group.add(wrist);

        this._addAxes(j4Group, 0.15);
        this.joints.push({ group: j4Group, axis: 'y', min: -Math.PI, max: Math.PI, speed: 0.6, phase: 0.3 });

        // J5: Wrist pitch (Z axis)
        const j5Group = new THREE.Group();
        j5Group.position.set(0, 0.06, 0);
        j4Group.add(j5Group);

        const wrist2Geo = new THREE.SphereGeometry(0.06, 12, 12);
        const wrist2 = new THREE.Mesh(wrist2Geo, jointMat);
        wrist2.castShadow = true;
        j5Group.add(wrist2);

        this._addAxes(j5Group, 0.12);
        this.joints.push({ group: j5Group, axis: 'z', min: -Math.PI * 0.3, max: Math.PI * 0.3, speed: 0.5, phase: 2.0 });

        // J6: Tool roll (Y axis)
        const j6Group = new THREE.Group();
        j6Group.position.set(0, 0.04, 0);
        j5Group.add(j6Group);

        this._addAxes(j6Group, 0.1);
        this.joints.push({ group: j6Group, axis: 'y', min: -Math.PI, max: Math.PI, speed: 0.8, phase: 1.5 });

        // Tool: welding torch
        const torchGeo = new THREE.CylinderGeometry(0.012, 0.03, 0.10, 8);
        const torchMat = new THREE.MeshStandardMaterial({ color: 0x4488ff, roughness: 0.3, metalness: 0.8 });
        const torch = new THREE.Mesh(torchGeo, torchMat);
        torch.position.y = 0.05;
        torch.castShadow = true;
        j6Group.add(torch);

        // TCP marker (small glowing sphere) + axes
        const tcpGeo = new THREE.SphereGeometry(0.015, 8, 8);
        const tcpMat = new THREE.MeshBasicMaterial({ color: 0x00ff88 });
        this.tcp = new THREE.Mesh(tcpGeo, tcpMat);
        this.tcp.position.y = 0.11;
        this._addAxes(this.tcp, 0.15);
        j6Group.add(this.tcp);

        // TCP trail
        const trailGeo = new THREE.BufferGeometry();
        const trailMat = new THREE.LineBasicMaterial({ color: 0x00ff88, transparent: true, opacity: 0.5 });
        this.trailLine = new THREE.Line(trailGeo, trailMat);
        scene.add(this.trailLine);
        this.trailPoints = [];
    },

    _addAxes(parent, size) {
        // Custom axes with solid colors (no gradient)
        const group = new THREE.Group();
        group.renderOrder = 999;

        const makeLine = (color, end) => {
            const geo = new THREE.BufferGeometry().setFromPoints([
                new THREE.Vector3(0, 0, 0), end
            ]);
            const mat = new THREE.LineBasicMaterial({ color, depthTest: false });
            return new THREE.Line(geo, mat);
        };

        group.add(makeLine(0xff0000, new THREE.Vector3(size, 0, 0))); // X = red
        group.add(makeLine(0x00ff00, new THREE.Vector3(0, size, 0))); // Y = green
        group.add(makeLine(0x0000ff, new THREE.Vector3(0, 0, size))); // Z = blue

        parent.add(group);
        this.axesHelpers.push(group);
        return group;
    },

    toggleAxes() {
        this.axesVisible = !this.axesVisible;
        for (const a of this.axesHelpers) a.visible = this.axesVisible;
        return this.axesVisible ? 'axes visible' : 'axes hidden';
    },

    _initSparkPool(scene, count) {
        const sparkGeo = new THREE.SphereGeometry(0.008, 4, 4);
        for (let i = 0; i < count; i++) {
            const mat = new THREE.MeshBasicMaterial({ color: 0xffcc00, transparent: true });
            const spark = new THREE.Mesh(sparkGeo, mat);
            spark.visible = false;
            scene.add(spark);
            this.sparkPool.push(spark);
        }
        this.sparks = [];
    },

    _emitSparks(origin, count) {
        for (let i = 0; i < count; i++) {
            const spark = this.sparkPool.find(s => !s.visible);
            if (!spark) break;

            spark.visible = true;
            spark.position.copy(origin);

            // Random velocity: mostly upward and outward
            const angle = Math.random() * Math.PI * 2;
            const upSpeed = 1.0 + Math.random() * 2.5;
            const outSpeed = 0.5 + Math.random() * 1.5;
            const vel = new THREE.Vector3(
                Math.cos(angle) * outSpeed,
                upSpeed,
                Math.sin(angle) * outSpeed
            );

            const maxLife = 0.3 + Math.random() * 0.6;
            // Spark color: bright yellow → orange → red
            const hue = 0.05 + Math.random() * 0.08;
            spark.material.color.setHSL(hue, 1.0, 0.7);
            spark.scale.setScalar(0.5 + Math.random() * 1.5);

            this.sparks.push({ mesh: spark, velocity: vel, life: maxLife, maxLife });
        }
    },

    _updateSparks(dt) {
        const gravity = new THREE.Vector3(0, -6, 0);
        for (let i = this.sparks.length - 1; i >= 0; i--) {
            const s = this.sparks[i];
            s.life -= dt;
            if (s.life <= 0) {
                s.mesh.visible = false;
                this.sparks.splice(i, 1);
                continue;
            }
            // Physics
            s.velocity.addScaledVector(gravity, dt);
            s.mesh.position.addScaledVector(s.velocity, dt);
            // Fade out + shrink
            const ratio = s.life / s.maxLife;
            s.mesh.material.opacity = ratio;
            s.mesh.scale.setScalar((0.5 + Math.random() * 0.5) * ratio);
            // Floor collision
            if (s.mesh.position.y < 0.01) {
                s.mesh.visible = false;
                this.sparks.splice(i, 1);
            }
        }
    },

    // Seam matches plate edges (plates span X 0.75 to 1.05)
    _seamStart: 0.75,
    _seamEnd: 1.05,
    _seamY: 0.44,  // top of plates
    _weldPath: null, // precomputed joint angles for each seam step

    // Precompute the weld path at init time using real TCP position feedback.
    // This guarantees the tool tip follows a perfectly straight line on the plates.
    _precomputeWeldPath() {
        const steps = 30;
        this._weldPath = [];
        const tmpVec = new THREE.Vector3();
        const target = new THREE.Vector3();
        let prev = null;
        let maxErr = 0;

        for (let i = 0; i <= steps; i++) {
            const t = i / steps;
            const targetX = this._seamStart + t * (this._seamEnd - this._seamStart);
            const targetY = this._seamY;
            const targetZ = 0.0;

            // Warm start: use previous step's solution
            const angles = this._solveIK(targetX, targetY, targetZ, tmpVec, prev);
            prev = angles;

            // Verify
            this._setJointAnglesRad(angles);
            this.scene.updateMatrixWorld(true);
            this.tcp.getWorldPosition(tmpVec);
            target.set(targetX, targetY, targetZ);
            const err = tmpVec.distanceTo(target);
            maxErr = Math.max(maxErr, err);

            console.log(`Weld step ${i}/${steps}: target(${targetX.toFixed(3)}, ${targetY.toFixed(3)}) → TCP(${tmpVec.x.toFixed(3)}, ${tmpVec.y.toFixed(3)}, ${tmpVec.z.toFixed(3)}) err=${(err*1000).toFixed(1)}mm`);

            this._weldPath.push(angles);
        }

        // Restore to home (facing table)
        this._setJointAnglesRad([Math.PI, 0.3, 0.0, 0, 0.3, 0]);
        console.log(`Weld path precomputed: ${this._weldPath.length} steps, max error: ${(maxErr*1000).toFixed(1)}mm`);
    },

    // Numerical IK: gradient descent to place TCP at (tx, ty, tz).
    // Adjusts J2, J3, J5 by measuring actual TCP world position each step.
    _solveIK(tx, ty, tz, tmpVec, prevAngles) {
        // J2 Z-rotation tilts arm toward local -X, so J1 must be offset by PI
        // to map local -X to the world direction of the target
        const j1 = Math.atan2(-tz, -tx);

        // Warm start from previous solution, or "reach over table" pose
        // J2 tilts upper arm forward, J3 positive folds forearm further forward/down
        let j2 = prevAngles ? prevAngles[1] : 1.0;
        let j3 = prevAngles ? prevAngles[2] : 0.6;
        let j5 = prevAngles ? prevAngles[4] : 0.5;
        const h = 0.002; // finite difference step
        const target = new THREE.Vector3(tx, ty, tz);

        const getErr = (a2, a3, a5) => {
            this._setJointAnglesRad([j1, a2, a3, 0, a5, 0]);
            this.scene.updateMatrixWorld(true);
            this.tcp.getWorldPosition(tmpVec);
            return tmpVec.distanceTo(target);
        };

        for (let i = 0; i < 800; i++) {
            const err = getErr(j2, j3, j5);
            if (err < 0.003) break;

            // Numerical gradient
            const g2 = (getErr(j2 + h, j3, j5) - getErr(j2 - h, j3, j5)) / (2 * h);
            const g3 = (getErr(j2, j3 + h, j5) - getErr(j2, j3 - h, j5)) / (2 * h);
            const g5 = (getErr(j2, j3, j5 + h) - getErr(j2, j3, j5 - h)) / (2 * h);

            // Normalize gradient to prevent tiny steps
            const gLen = Math.sqrt(g2 * g2 + g3 * g3 + g5 * g5);
            if (gLen < 1e-8) break;

            // Step size proportional to error
            const lr = Math.min(0.3, err) / gLen;
            j2 -= g2 * lr;
            j3 -= g3 * lr;
            j5 -= g5 * lr;

            // Clamp — arm reaches forward and folds down to table
            j2 = Math.max(0.3, Math.min(Math.PI * 0.55, j2));   // 17°–99° forward tilt (never past horizontal)
            j3 = Math.max(-0.3, Math.min(Math.PI * 0.7, j3));   // −17° to +126° — positive = fold forward/down
            j5 = Math.max(-0.3, Math.min(Math.PI * 0.7, j5));   // wrist pitch
        }

        const finalAngles = [j1, j2, j3, 0, j5, 0];
        this._setJointAnglesRad(finalAngles);
        this.scene.updateMatrixWorld(true);
        this.tcp.getWorldPosition(tmpVec);
        const finalErr = tmpVec.distanceTo(target);

        if (finalErr > 0.01) {
            console.warn('IK error:', (finalErr * 1000).toFixed(1) + 'mm',
                'target:', tx.toFixed(3), ty.toFixed(3),
                'j2:', j2.toFixed(3), 'j3:', j3.toFixed(3), 'j5:', j5.toFixed(3));
        }

        return finalAngles;
    },

    // Get interpolated angles from precomputed path
    _weldTargetAngles(progress) {
        if (!this._weldPath || this._weldPath.length === 0) {
            return [0, 0.2, -0.3, 0, 0, 0];
        }
        const steps = this._weldPath.length - 1;
        const idx = progress * steps;
        const i0 = Math.min(Math.floor(idx), steps);
        const i1 = Math.min(i0 + 1, steps);
        const frac = idx - i0;

        // Linear interpolation between precomputed steps
        const a0 = this._weldPath[i0];
        const a1 = this._weldPath[i1];
        return a0.map((v, i) => v + (a1[i] - v) * frac);
    },

    start() {
        if (this.animationId) return 'already running';
        this.clock.start();
        this.weldProgress = 0;
        this.isWelding = false;
        this.weldSeamPoints = [];
        this._phase = 'approach'; // approach → weld → retract → idle → repeat
        this._phaseTime = 0;

        const animate = () => {
            this.animationId = requestAnimationFrame(animate);
            const dt = this.clock.getDelta();
            const t = this.clock.getElapsedTime();
            this._phaseTime += dt;

            if (this._phase === 'approach') {
                // Move from home to weld start over 2 seconds
                const p = Math.min(this._phaseTime / 2.0, 1.0);
                const ease = p * p * (3 - 2 * p); // smoothstep
                const target = this._weldTargetAngles(0);
                const home = [Math.PI, 0.3, 0.0, 0, 0.3, 0];
                const angles = home.map((h, i) => h + (target[i] - h) * ease);
                this._setJointAnglesRad(angles);

                if (p >= 1.0) {
                    this._phase = 'weld';
                    this._phaseTime = 0;
                    this.isWelding = true;
                    this.weldSeamPoints = [];
                }
            } else if (this._phase === 'weld') {
                // Weld along the seam over 6 seconds
                const weldDuration = 6.0;
                this.weldProgress = Math.min(this._phaseTime / weldDuration, 1.0);
                const angles = this._weldTargetAngles(this.weldProgress);
                this._setJointAnglesRad(angles);

                // Weld point on the plate surface
                const weldX = this._seamStart + this.weldProgress * (this._seamEnd - this._seamStart);
                const weldPoint = new THREE.Vector3(weldX, this._seamY, 0);

                // Emit sparks at weld point on the plate
                this._emitSparks(weldPoint, 3 + Math.floor(Math.random() * 4));

                // Weld light flicker
                this.weldLight.position.copy(weldPoint);
                this.weldLight.intensity = 2.0 + Math.random() * 3.0;
                this.weldLight.color.setHSL(0.08 + Math.random() * 0.04, 1.0, 0.6);

                // Build weld seam on workpiece — perfectly straight along the gap
                const seamX = this._seamStart + this.weldProgress * (this._seamEnd - this._seamStart);
                const seamPoint = new THREE.Vector3(seamX, this._seamY + 0.001, 0);
                this.weldSeamPoints.push(seamPoint);
                this._updateWeldSeam();

                // Pulsing TCP glow (welding arc)
                const pulse = 0.5 + 0.5 * Math.sin(t * 30);
                this.tcp.material.color.setRGB(1.0, 0.7 + pulse * 0.3, pulse * 0.3);
                this.tcp.scale.setScalar(1.0 + pulse * 0.5);

                if (this.weldProgress >= 1.0) {
                    this._phase = 'retract';
                    this._phaseTime = 0;
                    this.isWelding = false;
                    this.weldLight.intensity = 0;
                    this.tcp.material.color.setRGB(0, 1, 0.5);
                    this.tcp.scale.setScalar(1.0);
                }
            } else if (this._phase === 'retract') {
                // Move back to home over 2 seconds
                const p = Math.min(this._phaseTime / 2.0, 1.0);
                const ease = p * p * (3 - 2 * p);
                const weldEnd = this._weldTargetAngles(1.0);
                const home = [Math.PI, 0.3, 0.0, 0, 0.3, 0];
                const angles = weldEnd.map((w, i) => w + (home[i] - w) * ease);
                this._setJointAnglesRad(angles);

                // Fade weld seam from orange to silver (cooling)
                if (this.weldSeam) {
                    const cool = p;
                    this._weldSeamMat.color.setRGB(1.0 - cool * 0.6, 0.4 + cool * 0.4, 0.13 + cool * 0.5);
                    this._weldSeamMat.emissiveIntensity = 0.3 * (1 - cool);
                }

                if (p >= 1.0) {
                    this._phase = 'idle';
                    this._phaseTime = 0;
                }
            } else if (this._phase === 'idle') {
                // Pause for 2 seconds, then repeat
                // Gentle idle sway
                const sway = Math.sin(t * 0.5) * 0.05;
                this._setJointAnglesRad([Math.PI + sway, 0.3, 0.0, 0, 0.3, 0]);

                if (this._phaseTime > 2.0) {
                    this._phase = 'approach';
                    this._phaseTime = 0;
                    // Reset seam for next pass (fresh weld)
                    // Remove old seam for fresh weld
                    if (this.weldSeam) {
                        this.scene.remove(this.weldSeam);
                        this.weldSeam.geometry.dispose();
                        this.weldSeam = null;
                    }
                    this._weldSeamMat.color.setRGB(1.0, 0.4, 0.13);
                    this._weldSeamMat.emissiveIntensity = 0.3;
                }
            }

            // Update spark particles
            this._updateSparks(dt);

            // Update TCP trail
            if (this.tcp) {
                const worldPos = new THREE.Vector3();
                this.tcp.getWorldPosition(worldPos);
                this.trailPoints.push(worldPos.clone());
                if (this.trailPoints.length > 500) this.trailPoints.shift();

                const positions = new Float32Array(this.trailPoints.length * 3);
                for (let i = 0; i < this.trailPoints.length; i++) {
                    positions[i * 3] = this.trailPoints[i].x;
                    positions[i * 3 + 1] = this.trailPoints[i].y;
                    positions[i * 3 + 2] = this.trailPoints[i].z;
                }
                this.trailLine.geometry.dispose();
                this.trailLine.geometry = new THREE.BufferGeometry();
                this.trailLine.geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
            }

            this.controls.update();
            this.renderer.render(this.scene, this.camera);
        };

        animate();
        return 'welding simulation started';
    },

    _setJointAnglesRad(angles) {
        for (let i = 0; i < Math.min(angles.length, this.joints.length); i++) {
            const j = this.joints[i];
            if (j.axis === 'y') j.group.rotation.y = angles[i];
            else if (j.axis === 'z') j.group.rotation.z = angles[i];
            else if (j.axis === 'x') j.group.rotation.x = angles[i];
        }
    },

    _updateWeldSeam() {
        if (this.weldProgress <= 0.01) return;
        const startX = this._seamStart;
        const endX = this._seamStart + this.weldProgress * (this._seamEnd - this._seamStart);
        const length = endX - startX;
        if (length < 0.005) return;

        // Remove old seam mesh
        if (this.weldSeam) {
            this.scene.remove(this.weldSeam);
            this.weldSeam.geometry.dispose();
        }

        // Create a box that fills the gap: width=gap(0.03), height=bead(0.015), length=progress
        const geo = new THREE.BoxGeometry(length, 0.015, 0.03);
        this.weldSeam = new THREE.Mesh(geo, this._weldSeamMat);
        this.weldSeam.position.set(
            startX + length / 2,  // center of bead
            this._seamY + 0.007,  // slightly raised above plate
            0                     // centered in gap
        );
        this.scene.add(this.weldSeam);
    },

    stop() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }
        this.isWelding = false;
        if (this.weldLight) this.weldLight.intensity = 0;
        // Hide all sparks
        for (const s of this.sparks) s.mesh.visible = false;
        this.sparks = [];
        return 'stopped';
    },

    // Set joint angles from C# (degrees)
    setJoints(angles) {
        for (let i = 0; i < Math.min(angles.length, this.joints.length); i++) {
            const rad = angles[i] * Math.PI / 180;
            const j = this.joints[i];
            if (j.axis === 'y') j.group.rotation.y = rad;
            else if (j.axis === 'z') j.group.rotation.z = rad;
            else if (j.axis === 'x') j.group.rotation.x = rad;
        }
        this.renderer.render(this.scene, this.camera);
        return 'joints set';
    },

    resize() {
        const canvas = this.renderer.domElement;
        const w = canvas.clientWidth;
        const h = canvas.clientHeight;
        this.renderer.setSize(w, h, false);
        this.camera.aspect = w / h;
        this.camera.updateProjectionMatrix();
    },

    dispose() {
        this.stop();
        if (this.renderer) {
            this.renderer.dispose();
            this.renderer = null;
        }
        this.scene = null;
        this.camera = null;
        this.controls = null;
        this.joints = [];
    },
};
