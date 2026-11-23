using UnityEngine;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        [Header("Riverside encounter (one step = 4 world units)")]
        public bool RiversideDemo = true;
        [Min(1)] public int RiversidePlayerHealth = 5, RiversideBoarHealth = 3;
        [Min(.1f)] public float AxeInterval = .65f, AxeRangeSteps = .55f;
        public float FatherStartSteps = 1.8f, FatherStopSteps = 1.2f, FatherSpeedRatio = 1.15f;
        public float BoarWarning = .8f, BoarLockTime = .4f, BoarChargeSteps = 1.8f, BoarChargeSeconds = .75f;
        public float BoarRestSeconds = 1.4f, BoarLeashSteps = 4, BoarLeashSeconds = 2;
        public float DeerSpeedRatio = 2.2f, DeerFastSeconds = 1.4f, DeerBrakeSeconds = .6f;
        public float DeerSafeSteps = 3, DeerAlertSteps = 2, DeerLookSeconds = 1.5f;
        const float RiversideStep = 4;
        Being riversideBoar, riversideDeer;
        enum BoarPhase { Idle, Warning, Charge, Rest, Return }
        BoarPhase boarPhase;
        float boarTimer, leashTimer, deerTimer, deerDuration, deerLookTimer;
        bool chargeHit, fatherFollowing, fatherWarned, deerRunning;
        Vector3 chargeDirection, deerDirection, deerDestination;
        AudioSource deerVoice;
        Sprite antlerGlyph;
        Sprite featherGlyph;
        readonly Being[] riversideChickens = new Being[2];
        readonly float[] chickenPursuit = new float[2], chickenQuiet = new float[2], chickenReady = new float[2];
        readonly Being[] chickenCover = new Being[2];
        AudioClip chickenMourning;
        LineRenderer deerTear;
        float deerDeathTime;

        void SetupRiverside()
        {
            if (!RiversideDemo || !XRTrackingControlsCamera) return;
            foreach (var resident in animals) resident.root.gameObject.SetActive(false);
            animals.Clear();
            AddDictionaryAnimal(new Vector3(-15, 0, -3), 9);
            riversideBoar = animals[animals.Count - 1];
            AddDictionaryAnimal(new Vector3(-9, 0, 6), 8);
            riversideDeer = animals[animals.Count - 1];
            riversideBoar.root.name = "Riverside boar";
            riversideDeer.root.name = "Riverside deer";
            var deerSprite = DrawRiverDeer();
            riversideDeer.renderer.sprite = deerSprite;
            riversideDeer.initialScale = riversideDeer.art.localScale =
                Vector3.one * (1.25f * XRArtworkScale / deerSprite.bounds.size.y);
            riversideBoar.health = RiversideBoarHealth;
            riversideDeer.health = 3;
            health = RiversidePlayerHealth;
            var chickenGlyph = DrawRiverChicken();
            for (int i = 0; i < riversideChickens.Length; i++)
            {
                AddDictionaryAnimal(new Vector3(-6 + i * 2, 0, -2.8f + i * 2.6f), 3);
                var chicken = animals[animals.Count - 1];
                chicken.root.name = "Riverside chicken";
                chicken.renderer.sprite = chickenGlyph;
                chicken.initialScale = chicken.art.localScale =
                    Vector3.one * (.52f * XRArtworkScale / chickenGlyph.bounds.size.y);
                chicken.health = 1;
                riversideChickens[i] = chicken;
            }
            deerVoice = riversideDeer.root.gameObject.AddComponent<AudioSource>();
            deerVoice.spatialBlend = 0;
            var samples = new float[26460];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / 22050f;
                float pitch = 520 - 180 * t + 70 * Mathf.Sin(t * 35);
                samples[i] = Mathf.Sin(2 * Mathf.PI * pitch * t) * Mathf.Sin(Mathf.PI * i / samples.Length) * .18f;
            }
            deerVoice.clip = AudioClip.Create("Procedural deer alarm", samples.Length, 1, 22050, false);
            deerVoice.clip.SetData(samples, 0);
            chickenMourning = MakeRiverCall("Soft chicken call", 410, .22f);
        }

        bool IsRiversideChicken(Being animal) => animal != null &&
            (animal == riversideChickens[0] || animal == riversideChickens[1]);

        bool RiversideAnimal(Being animal) => animal == riversideBoar || animal == riversideDeer || IsRiversideChicken(animal);

        void HitRiverside(Being animal)
        {
            if (IsRiversideChicken(animal))
            {
                if (animal.health <= 0) return;
                ApplyEcologyHit(animal);
                animal.art.rotation = billboard * Quaternion.Euler(0, 0, -80);
                if (featherGlyph == null) featherGlyph = DrawFeather();
                var feather = Billboard("Dropped feather", null, featherGlyph, .32f);
                feather.position = animal.root.position + Vector3.right * .45f;
                looseWood.Add(feather);
                featherDrops.Add(feather);
                var meat = Billboard("Chicken meat left behind", null, RiverGlyph(
                    InkStroke(20,40, 43,70, 74,73, 98,47, 81,32, 44,32, 20,40)), .21f);
                meat.position = animal.root.position - Vector3.right * .35f;
                meat.GetComponent<SpriteRenderer>().color = new Color(.71f, .4f, .32f);
                return;
            }
            if (animal == riversideDeer)
            {
                if (animal.health <= 0) return;
                ApplyEcologyHit(animal);
                if (animal.health <= 0)
                {
                    deerRunning = false;
                    deerVoice.Stop();
                    animal.art.rotation = billboard * Quaternion.Euler(0, 0, -72);
                    if (antlerGlyph == null) antlerGlyph = DrawAntler();
                    var drop = Billboard("Dropped antler", null, antlerGlyph, .39f);
                    drop.position = animal.root.position;
                    looseWood.Add(drop);
                    antlerDrops.Add(drop);
                    deerDeathTime = clock;
                    var tear = new GameObject("Deer ink tear");
                    tear.transform.SetParent(animal.art, false);
                    deerTear = tear.AddComponent<LineRenderer>();
                    deerTear.sharedMaterial = firstAxeInk;
                    deerTear.useWorldSpace = false;
                    deerTear.positionCount = 2;
                    deerTear.startWidth = .032f;
                    deerTear.endWidth = .01f;
                    deerTear.startColor = deerTear.endColor = new Color(.61f, .09f, .12f);
                    deerTear.sortingOrder = animal.renderer.sortingOrder + 2;
                    foreach (var chicken in riversideChickens)
                    {
                        if (chicken.health <= 0 || Vector3.Distance(chicken.root.position, animal.root.position) > 8) continue;
                        chicken.renderer.flipX = chicken.root.position.x > animal.root.position.x;
                        var voice = chicken.root.gameObject.AddComponent<AudioSource>();
                        voice.PlayOneShot(chickenMourning, .45f);
                        chicken.cooldown = 1.5f;
                    }
                    return;
                }
                if (deerRunning) return;
                if (!deerVoice.isPlaying) deerVoice.Play();
                StartDeerRun(DeerFastSeconds + DeerBrakeSeconds);
                return;
            }
            ApplyEcologyHit(animal);
            if (animal.health <= 0)
            {
                if (riverTask == RiverTask.Boar)
                {
                    animal.health = 1;
                    boarPhase = BoarPhase.Return;
                    AdvanceRiverTask(RiverTask.Return);
                }
                else animal.root.gameObject.SetActive(false);
            }
            else if (boarPhase == BoarPhase.Idle || boarPhase == BoarPhase.Return) BeginBoarWarning();
        }

        void BeginBoarWarning()
        {
            boarPhase = BoarPhase.Warning;
            boarTimer = 0;
            chargeHit = false;
            chargeDirection = (Feet - riversideBoar.root.position).normalized;
            if (!fatherWarned)
            {
                fatherWarned = true;
                Tell("Father: Dodge to the side!");
                if (storyFatherRoot != null)
                    MoveStoryFatherToward(Feet + Vector3.Cross(Vector3.up, chargeDirection) * 4, 8, .5f);
            }
        }

        void StartDeerRun(float duration)
        {
            deerRunning = true;
            deerTimer = 0;
            deerDuration = duration;
            Vector3 start = riversideDeer.root.position;
            Vector3 away = Vector3.ProjectOnPlane(start - Feet, Vector3.up).normalized;
            if (away == Vector3.zero) away = Vector3.forward;
            deerDirection = Vector3.zero;
            deerDestination = start;
            float speed = RiversideStep * DeerSpeedRatio;
            float desiredDistance = speed * (duration - .5f * Mathf.Min(duration, DeerBrakeSeconds));
            float bestScore = 0;
            for (int i = 0; i < 9; i++)
            {
                float angle = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 22.5f;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * away;
                float clearDistance = 0;
                // Keep the flight on dry ground and shorten it when the bank is crowded.
                for (float step = .2f; step <= desiredDistance; step += .2f)
                {
                    if (!ClearAnimalStep(start + direction * step, riversideDeer)) break;
                    clearDistance = step;
                }
                float score = clearDistance * (.6f + .4f * Vector3.Dot(direction, away));
                if (score <= bestScore) continue;
                bestScore = score;
                deerDirection = direction;
                deerDestination = start + direction * clearDistance;
            }
            if (deerDirection == Vector3.zero) { deerRunning = false; deerLookTimer = DeerLookSeconds; }
        }

        void UpdateRiversideAnimal(Being animal, float dt)
        {
            if (animal == riversideDeer && deerTear != null)
            {
                float progress = Mathf.Clamp01((clock - deerDeathTime) / 1.4f);
                var start = new Vector3(.28f, .78f, -.01f);
                deerTear.SetPosition(0, start);
                deerTear.SetPosition(1, start + Vector3.down * (.22f * progress));
                deerTear.enabled = progress < 1;
            }
            if (animal.health <= 0 || fatherIntroActive || !XRExperienceReady) return;
            if (IsRiversideChicken(animal))
            {
                if (animal.cooldown > 0) { animal.cooldown -= dt; return; }
                int index = animal == riversideChickens[0] ? 0 : 1;
                var cover = chickenCover[index];
                float chickenDistance = Vector3.Distance(Feet, animal.root.position);
                if (cover != null)
                {
                    chickenQuiet[index] = chickenDistance > 2.4f ? chickenQuiet[index] + dt : 0;
                    if (chickenQuiet[index] >= 5)
                    {
                        ShakeRiversideCover(cover, false);
                        chickenCover[index] = null;
                        animal.renderer.enabled = true;
                        chickenReady[index] = clock + 8;
                    }
                    return;
                }
                chickenPursuit[index] = chickenDistance < 2.6f ? chickenPursuit[index] + dt : 0;
                if (chickenDistance < 2.6f)
                {
                    var away = Vector3.ProjectOnPlane(animal.root.position - Feet, Vector3.up).normalized;
                    var next = animal.root.position + away * (2f * dt);
                    if (ClearAnimalStep(next, animal)) animal.root.position = next;
                    animal.renderer.flipX = away.x < 0;
                }
                if (chickenPursuit[index] >= 1.5f && clock >= chickenReady[index])
                {
                    foreach (var plant in plants)
                    {
                        if (plant.tree || plant.targetScale < .5f || Vector3.Distance(plant.home, animal.root.position) > 1.25f) continue;
                        if (!Free(plant.home)) continue;
                        ShakeRiversideCover(plant, true);
                        chickenCover[index] = plant;
                        chickenQuiet[index] = 0;
                        animal.root.position = plant.home;
                        animal.renderer.enabled = false;
                        break;
                    }
                    chickenPursuit[index] = 0;
                }
                float peck = Mathf.Pow(Mathf.Max(0, Mathf.Sin(clock * 4 + animal.phase)), 4);
                animal.art.rotation = billboard * Quaternion.Euler(0, 0, peck * 12);
                return;
            }
            if (animal == riversideDeer)
            {
                if (deerRunning)
                {
                    deerTimer += dt;
                    float brake = Mathf.Clamp01((deerDuration - deerTimer) / Mathf.Max(.01f, DeerBrakeSeconds));
                    Vector3 next = Vector3.MoveTowards(animal.root.position, deerDestination,
                        RiversideStep * DeerSpeedRatio * brake * dt);
                    bool blocked = !ClearAnimalStep(next, animal);
                    if (!blocked) animal.root.position = next;
                    else deerTimer = deerDuration;
                    animal.renderer.flipX = deerDirection.x < 0;
                    if (deerTimer >= deerDuration)
                    {
                        deerRunning = false;
                        deerLookTimer = DeerLookSeconds;
                        if (!blocked && (animal.root.position - deerDestination).sqrMagnitude > .09f
                            && Vector3.Distance(Feet, animal.root.position) < DeerSafeSteps * RiversideStep)
                            StartDeerRun(1);
                    }
                }
                else
                {
                    deerLookTimer = Mathf.Max(0, deerLookTimer - dt);
                    animal.renderer.flipX = Feet.x < animal.root.position.x;
                    if (deerLookTimer > 0 && Vector3.Distance(Feet, animal.root.position) < DeerAlertSteps * RiversideStep)
                        StartDeerRun(1.2f);
                }
                animal.art.localPosition = Vector3.up * (deerRunning ? Mathf.Abs(Mathf.Sin(clock * 18)) * .15f : 0);
                return;
            }
            if (boarPhase == BoarPhase.Idle)
            {
                if (riverTask == RiverTask.Boar && Vector3.Distance(Feet, animal.root.position) < 5.2f) BeginBoarWarning();
                return;
            }
            float distance = Vector3.Distance(Feet, animal.root.position);
            leashTimer = distance > BoarLeashSteps * RiversideStep ? leashTimer + dt : 0;
            if (leashTimer >= BoarLeashSeconds) boarPhase = BoarPhase.Return;
            boarTimer += dt;
            if (boarPhase == BoarPhase.Warning)
            {
                if (boarTimer < BoarWarning - BoarLockTime) chargeDirection = (Feet - animal.root.position).normalized;
                animal.renderer.color = new Color(1, .55f, .35f);
                animal.art.localPosition = Vector3.up * (Mathf.Sin(clock * 35) * .07f);
                if (boarTimer >= BoarWarning) { boarPhase = BoarPhase.Charge; boarTimer = 0; }
            }
            else if (boarPhase == BoarPhase.Charge)
            {
                Vector3 previous = animal.root.position;
                Vector3 next = previous + chargeDirection * (BoarChargeSteps * RiversideStep / Mathf.Max(.01f, BoarChargeSeconds) * dt);
                if (ClearAnimalStep(next, animal)) animal.root.position = next;
                Vector3 segment = animal.root.position - previous;
                float fraction = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(Feet - previous, segment) / segment.sqrMagnitude) : 0;
                if (!chargeHit && Vector3.Distance(Feet, previous + segment * fraction) < .85f)
                {
                    chargeHit = true;
                    health--;
                    damageCooldown = 1;
                    var rightController = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
                    if (rightController.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                        rightController.SendHapticImpulse(0, .25f, .12f);
                    Tell("The boar hit you.");
                    if (health <= 0)
                    {
                        player.position = ForestSpawn;
                        lastDryPosition = ForestSpawn;
                        health = RiversidePlayerHealth;
                        animal.root.position = animal.home;
                        animal.health = RiversideBoarHealth;
                        boarPhase = BoarPhase.Idle;
                        return;
                    }
                }
                if (boarTimer >= BoarChargeSeconds) { boarPhase = BoarPhase.Rest; boarTimer = 0; }
            }
            else if (boarPhase == BoarPhase.Rest && boarTimer >= BoarRestSeconds) BeginBoarWarning();
            else if (boarPhase == BoarPhase.Return)
            {
                if (!HasAnimalRoute(animal)) PlanAnimalRoute(animal, animal.home);
                if (HasAnimalRoute(animal))
                {
                    var goal = animal.route[animal.routeIndex];
                    var next = Vector3.MoveTowards(animal.root.position, goal, RiversideStep * dt);
                    if (ClearAnimalStep(next, animal)) animal.root.position = next;
                    if (Vector3.Distance(next, goal) < .12f) animal.routeIndex++;
                }
                if (Vector3.Distance(animal.root.position, animal.home) < .5f) boarPhase = BoarPhase.Idle;
            }
            animal.renderer.flipX = chargeDirection.x < 0;
            if (boarPhase != BoarPhase.Warning) { animal.renderer.color = Color.white; animal.art.localPosition = Vector3.zero; }
        }

        void ShakeRiversideCover(Being cover, bool shedLeaves)
        {
            grassCoverShake[cover] = clock;
            if (!shedLeaves || firstAxeTeachingTrees[0] == null) return;
            for (int i = 0; i < 4; i++)
                SpawnFallingLeaf(firstAxeTeachingTrees[0], i, cover.home + Vector3.up * .65f);
        }

        bool FollowRiversideFather(float dt)
        {
            if (riversideBoar == null) return false;
            if (!XRExperienceReady) return true;
            float distance = Vector3.Distance(Feet, storyFatherRoot.position);
            if (distance > FatherStartSteps * RiversideStep) fatherFollowing = true;
            if (distance <= FatherStopSteps * RiversideStep) fatherFollowing = false;
            if (fatherFollowing) MoveStoryFatherToward(Feet - heading.normalized * (FatherStopSteps * RiversideStep - .5f),
                RiversideStep * FatherSpeedRatio, dt);
            return true;
        }
    }
}
