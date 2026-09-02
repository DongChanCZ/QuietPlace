using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class LabEntranceGame : MonoBehaviour
{
    public bool buildWorldOnAwake = true;
    public static LabEntranceGame Instance { get; private set; }
    CharacterController player; Camera cam; Light flashlight; ParticleSystem collapseDust; GameObject brokenEntranceDoor, securityDoorVisual, collapsedEntranceBlocker;
    readonly List<GameObject> entrance = new(); readonly List<GameObject> rubble = new();
    Material concrete, dark, metal, wall, red, cyan, paper, black;
    float pitch; bool entranceOpened, collapsed, hasCard, readMap, mapOpen, doorOpened, secretOpened, stage2Reached, storyOpen;
    string objective = "연구소 정문으로 접근하세요"; string message = ""; float messageUntil;

    void Awake()
    {
        Instance = this;
        if (buildWorldOnAwake) { MakeMaterials(); MakeWorld(); } else CacheSceneReferences();
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    public void BuildSceneNow() { MakeMaterials(); MakeWorld(); }

    void CacheSceneReferences()
    {
        player = GameObject.Find("Player").GetComponent<CharacterController>(); cam = Camera.main; flashlight = cam.GetComponent<Light>();
        entrance.Clear(); rubble.Clear();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "Main Entrance Door Visual" || t.name.StartsWith("Entrance Door Leaf") || t.name == "Entrance Door Blocker") entrance.Add(t.gameObject);
            if (t.name == "Door Debris" || t.name == "Rubble" || t.name.StartsWith("Rubble Containment")) rubble.Add(t.gameObject);
            if (t.name == "Broken Entrance Door Visual") brokenEntranceDoor=t.gameObject;
            if (t.name == "Security Door Visual") securityDoorVisual=t.gameObject;
            if (t.name == "Collapsed Entrance Blocker") collapsedEntranceBlocker=t.gameObject;
        }
        var dust = GameObject.Find("Collapse Dust"); if (dust != null) collapseDust = dust.GetComponent<ParticleSystem>();
    }

    Material MakeMat(Color c, bool glow = false) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c }; if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 3f); } return m; }
    void MakeMaterials() { concrete = MakeMat(new Color(.24f,.26f,.27f)); dark=MakeMat(new Color(.055f,.065f,.075f)); metal=MakeMat(new Color(.1f,.12f,.13f)); wall=MakeMat(new Color(.32f,.34f,.34f)); red=MakeMat(new Color(.65f,.02f,.01f),true); cyan=MakeMat(new Color(.02f,.65f,.8f),true); paper=MakeMat(new Color(.62f,.58f,.4f)); black=MakeMat(new Color(.005f,.007f,.01f)); }

    void MakeWorld()
    {
        RenderSettings.fog=true; RenderSettings.fogColor=new Color(.02f,.035f,.05f); RenderSettings.fogMode=FogMode.Exponential; RenderSettings.fogDensity=.025f; RenderSettings.skybox=null; RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.018f,.025f,.035f);
        var moon=new GameObject("Moon Light").AddComponent<Light>(); moon.type=LightType.Directional; moon.intensity=.42f; moon.color=new Color(.32f,.44f,.58f); moon.shadows=LightShadows.Soft; moon.shadowStrength=.55f; moon.transform.rotation=Quaternion.Euler(45,-30,0);
        Box("Ground",new Vector3(0,-.5f,10),new Vector3(46,1,64),dark); MakeReferenceFirstFloor(); MakePlayer(); MakeRain();
    }

    void MakeReferenceFirstFloor()
    {
        // Final reference plan: central lobby, west security/infirmary,
        // east document/warning areas, emergency stairs and elevator.
        Box("1F Floor",new Vector3(0,.06f,20),new Vector3(30,.12f,18),concrete);
        Box("1F Roof",new Vector3(0,5.3f,20),new Vector3(30.5f,.3f,18.5f),dark);
        Box("Exterior Back",new Vector3(0,2.65f,29),new Vector3(30,5.3f,.45f),concrete);
        // Left exterior wall is split around the guard-room vault window.
        Box("Exterior Left South",new Vector3(-15,2.65f,17.75f),new Vector3(.45f,5.3f,13.5f),concrete);
        Box("Exterior Left North",new Vector3(-15,2.65f,28.15f),new Vector3(.45f,5.3f,1.7f),concrete);
        Box("Exterior Left Window Sill",new Vector3(-15,.65f,25.9f),new Vector3(.45f,1.3f,2.8f),concrete);
        Box("Exterior Left Window Lintel",new Vector3(-15,4.33f,25.9f),new Vector3(.45f,1.95f,2.8f),concrete);
        Box("Exterior Right",new Vector3(15,2.65f,20),new Vector3(.45f,5.3f,18),concrete);
        Box("Front Wall Left",new Vector3(-8.3f,2.65f,11),new Vector3(13.4f,5.3f,.5f),concrete);
        Box("Front Wall Right",new Vector3(8.3f,2.65f,11),new Vector3(13.4f,5.3f,.5f),concrete);
        Box("Facade Band",new Vector3(0,4.95f,10.78f),new Vector3(30.5f,.7f,.72f),metal);
        MakeAbandonedHospitalExterior();

        // Central main entrance. Only these leaves are in the collapse list.
        Vector3 doorCenter=new Vector3(0,2.05f,10.72f);
        Box("Door Frame Left",doorCenter+new Vector3(-2.05f,.1f,.05f),new Vector3(.35f,4.4f,.75f),metal);
        Box("Door Frame Right",doorCenter+new Vector3(2.05f,.1f,.05f),new Vector3(.35f,4.4f,.75f),metal);
        Box("Door Frame Lintel",doorCenter+new Vector3(0,2.2f,.05f),new Vector3(4.4f,.35f,.75f),metal);
        var leftDoor=Box("Entrance Door Leaf L",doorCenter+new Vector3(-.92f,0,-.08f),new Vector3(1.76f,3.85f,.18f),metal);
        var rightDoor=Box("Entrance Door Leaf R",doorCenter+new Vector3(.92f,0,-.08f),new Vector3(1.76f,3.85f,.18f),metal);
        entrance.Add(leftDoor); entrance.Add(rightDoor);
        var blocker=Box("Entrance Door Blocker",doorCenter,new Vector3(3.65f,3.9f,.12f),metal); blocker.GetComponent<Renderer>().enabled=false; entrance.Add(blocker);
        var collapseBlocker=Box("Collapsed Entrance Blocker",new Vector3(0,2f,11.65f),new Vector3(4.2f,4f,.25f),concrete); collapseBlocker.GetComponent<Renderer>().enabled=false; collapseBlocker.isStatic=false; collapseBlocker.SetActive(false); collapsedEntranceBlocker=collapseBlocker;
        var entryLamp=Box("Entrance Lamp",new Vector3(0,4.55f,11.2f),new Vector3(1.7f,.1f,.2f),cyan); AddPoint(entryLamp.transform.position,Color.cyan,1.5f,5.5f);

        // Plan-accurate room edges. Four wall segments leave separate door gaps
        // into the west office/security rooms and east warning/document rooms.
        Box("West Lobby Wall South",new Vector3(-7,2.6f,14.5f),new Vector3(.25f,5.2f,7.0f),wall);
        Box("West Lobby Wall Center",new Vector3(-7,2.6f,20.0f),new Vector3(.25f,5.2f,1.2f),wall);
        Box("West Lobby Wall North",new Vector3(-7,2.6f,25.5f),new Vector3(.25f,5.2f,7.0f),wall);
        Box("Office Door Lintel",new Vector3(-7,4.25f,18.7f),new Vector3(.25f,1.9f,1.4f),wall);
        Box("Security Door Lintel",new Vector3(-7,4.25f,21.3f),new Vector3(.25f,1.9f,1.4f),wall);
        Box("Office Door Frame South",new Vector3(-7,1.65f,17.95f),new Vector3(.34f,3.3f,.12f),metal);
        Box("Office Door Frame North",new Vector3(-7,1.65f,19.45f),new Vector3(.34f,3.3f,.12f),metal);
        Box("Office Door Frame Top",new Vector3(-7,3.3f,18.7f),new Vector3(.34f,.16f,1.62f),metal);
        var officeDoor=Box("Office Door Visual",new Vector3(-7.55f,1.5f,18.45f),new Vector3(.14f,3f,1.25f),metal); officeDoor.transform.rotation=Quaternion.Euler(0,68,0); officeDoor.isStatic=false;
        Box("Security Door Frame South",new Vector3(-7,1.65f,20.55f),new Vector3(.34f,3.3f,.12f),metal);
        Box("Security Door Frame North",new Vector3(-7,1.65f,22.05f),new Vector3(.34f,3.3f,.12f),metal);
        Box("Security Door Frame Top",new Vector3(-7,3.3f,21.3f),new Vector3(.34f,.16f,1.62f),metal);
        Box("West Room Divider",new Vector3(-11,2.6f,20),new Vector3(8,5.2f,.25f),wall);
        // The document and bloody-warning areas are open parts of the lobby in the reference.
        Box("Core Wall South",new Vector3(11.5f,2.6f,12.6f),new Vector3(.25f,5.2f,3.2f),wall);
        Box("Core Wall Middle",new Vector3(11.5f,2.6f,19f),new Vector3(.25f,5.2f,6f),wall);
        Box("Core Wall North",new Vector3(11.5f,2.6f,26.4f),new Vector3(.25f,5.2f,5.2f),wall);
        Box("Elevator Entry Lintel",new Vector3(11.5f,4.25f,15.1f),new Vector3(.25f,1.9f,1.8f),wall);
        Box("Stair Entry Lintel",new Vector3(11.5f,4.25f,22.9f),new Vector3(.25f,1.9f,1.8f),wall);
        Box("Stair Elevator Divider",new Vector3(13.25f,2.6f,20),new Vector3(3.5f,5.2f,.25f),wall);

        // Reference reception: C-shaped counter opening toward the east/right side.
        Box("Reception Spine",new Vector3(-1.8f,1.05f,20),new Vector3(.75f,1.1f,5.0f),metal);
        Box("Reception North",new Vector3(.35f,1.05f,22.1f),new Vector3(4.3f,1.1f,.75f),metal);
        Box("Reception South",new Vector3(.35f,1.05f,17.9f),new Vector3(4.3f,1.1f,.75f),metal);
        Box("Lobby Bench North",new Vector3(5, .55f,26.8f),new Vector3(3.4f,1.1f,.75f),dark);
        Box("Lobby Bench South",new Vector3(5, .55f,13.2f),new Vector3(3.4f,1.1f,.75f),dark);

        // Security room gameplay props.
        Vector3 security=new Vector3(-10.5f,0,24.2f);
        Box("Desk",security+new Vector3(0,1.1f,1.5f),new Vector3(4.6f,.18f,1.2f),metal);
        Box("CRT",security+new Vector3(-1.1f,1.7f,1.55f),new Vector3(1.1f,.8f,.7f),black);
        var card=Box("Access Card",security+new Vector3(.9f,1.35f,1.3f),new Vector3(.24f,.025f,.15f),cyan); card.isStatic=false; card.AddComponent<AccessCard>();
        var securityDoor=Box("Security Door",new Vector3(-7,1.5f,21.3f),new Vector3(.18f,3,1.35f),metal); securityDoor.isStatic=false; securityDoor.AddComponent<SecurityDoor>();
        MakeSecurityVaultWindow();
        var map=Box("1F Laboratory Map",new Vector3(-10.5f,3.15f,28.70f),new Vector3(2.35f,1.45f,.06f),black); map.AddComponent<MapPanel>();
        // Bloody warning is fixed beside the vaultable window without blocking it.
        var warning=Box("Blood Warning",new Vector3(-14.70f,2.35f,23.65f),new Vector3(.035f,1.05f,.72f),paper); warning.AddComponent<StoryNote>();
        Box("Blood Mark",new Vector3(-14.67f,2.34f,23.65f),new Vector3(.025f,.68f,.13f),red).AddComponent<StoryNote>();

        // The supplied 1F plan has an office below the security room, not an infirmary.
        Box("Office Desk",new Vector3(-11.2f,.78f,15.5f),new Vector3(4.4f,1.55f,1.15f),metal);
        Box("Office Cabinet",new Vector3(-14.1f,1.35f,17.8f),new Vector3(1.25f,2.7f,3.0f),dark);
        Box("Office Chair",new Vector3(-10.8f,.55f,17.0f),new Vector3(.85f,1.1f,.85f),dark);
        Box("Office Sofa",new Vector3(-11.4f,.55f,12.3f),new Vector3(3.5f,1.1f,.85f),dark);
        Box("Office Side Cabinet",new Vector3(-8.1f,1.05f,12.2f),new Vector3(1.2f,2.1f,.65f),metal);
        Box("Torn Documents",new Vector3(9.2f,.05f,24.3f),new Vector3(.8f,.03f,.6f),paper).AddComponent<StoryNote>();
        Box("Document Bench",new Vector3(8.7f,.55f,27.6f),new Vector3(3.8f,1.1f,.7f),dark);
        Box("Warning Area Cabinet",new Vector3(9.8f,1.05f,12.1f),new Vector3(2f,2.1f,.65f),metal);
        Box("Lobby Planter Left",new Vector3(-4.8f,.65f,27.6f),new Vector3(.8f,1.3f,.8f),dark);
        Box("Lobby Planter Right",new Vector3(4.8f,.65f,27.6f),new Vector3(.8f,1.3f,.8f),dark);

        // Emergency stair and elevator core on the far right.
        for(int i=0;i<6;i++) Box("Emergency Stair Step",new Vector3(13.2f,.2f+i*.32f,23.1f+i*.55f),new Vector3(3,.32f,.6f),metal);
        Box("Elevator Door L",new Vector3(11.42f,1.7f,14.68f),new Vector3(.16f,3.4f,.72f),metal);
        Box("Elevator Door R",new Vector3(11.42f,1.7f,15.52f),new Vector3(.16f,3.4f,.72f),metal);
        AddFirstFloorMolding();
        var stage=new GameObject("Stage 2 Trigger"); stage.transform.position=new Vector3(13.2f,1.2f,25.4f); var sc=stage.AddComponent<BoxCollider>(); sc.isTrigger=true; sc.size=new Vector3(3,2.4f,1); stage.AddComponent<Stage2Zone>();

        // Local door-only collapse fragments.
        for(int i=0;i<12;i++)
        {
            float x=(i%4-1.5f)*.55f, y=1.05f+(i/4)*.7f, z=10.72f+(i%2)*.14f;
            var chunk=Box("Door Debris",new Vector3(x,y,z),new Vector3(.5f+(i%3)*.12f,.46f+(i%2)*.18f,.42f+(i%4)*.08f),concrete);
            chunk.isStatic=false; chunk.transform.rotation=Quaternion.Euler(i*13%28,i*31%55,i*19%24);
            var body=chunk.AddComponent<Rigidbody>(); body.mass=2.5f+i*.18f; body.linearDamping=.35f; body.angularDamping=.45f;
            chunk.SetActive(false); rubble.Add(chunk);
        }
        var dustGo=new GameObject("Collapse Dust"); dustGo.transform.position=new Vector3(0,1.8f,10.7f); collapseDust=dustGo.AddComponent<ParticleSystem>(); var dm=collapseDust.main; dm.playOnAwake=false; dm.duration=.7f; dm.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.85f); dm.startSpeed=new ParticleSystem.MinMaxCurve(.35f,1.3f); dm.startSize=new ParticleSystem.MinMaxCurve(.025f,.1f); dm.maxParticles=65; dm.startColor=new Color(.3f,.32f,.34f,.48f); var de=collapseDust.emission; de.rateOverTime=0; de.SetBursts(new[]{new ParticleSystem.Burst(0,48)});
        var zone=new GameObject("Collapse Trigger"); zone.transform.position=new Vector3(0,1.2f,8.9f); var col=zone.AddComponent<BoxCollider>(); col.isTrigger=true; col.size=new Vector3(3.8f,3,1.2f); zone.AddComponent<CollapseZone>();

        // Mood-reference facade fans and approach.
        for(int y=0;y<2;y++){ Cylinder("Facade Fan",new Vector3(8.8f,2f+y*2.1f,10.68f),new Vector3(1.25f,.15f,1.25f),metal,Quaternion.Euler(90,0,0)); for(int i=-2;i<=2;i++) Box("Fan Slat",new Vector3(8.8f,2f+y*2.1f+i*.22f,10.48f),new Vector3(2.4f,.07f,.08f),dark); }
        AddPoint(new Vector3(0,4.45f,17),new Color(.34f,.58f,.68f),1.65f,8.5f);
        AddPoint(new Vector3(0,4.45f,24),new Color(.28f,.52f,.62f),1.45f,8.5f);
        AddPoint(new Vector3(-10.5f,4.25f,24),new Color(.78f,.52f,.30f),1.35f,6.5f);
        AddPoint(new Vector3(-10.5f,4.25f,15.5f),new Color(.62f,.55f,.44f),1.05f,6f);
        AddPoint(new Vector3(9.2f,4.25f,24),new Color(.48f,.58f,.62f),.95f,5.5f);
        Box("Approach Road",new Vector3(0,.015f,-1),new Vector3(6,.03f,24),black);
        InvisibleBlock("Boundary Left",new Vector3(-20,2,5),new Vector3(.4f,4,38)); InvisibleBlock("Boundary Right",new Vector3(20,2,5),new Vector3(.4f,4,38)); InvisibleBlock("Boundary Back",new Vector3(0,2,-14),new Vector3(40,4,.4f));
        // Other floors stay disabled until the 1F blueprint blockout is approved.
    }

    void AddFirstFloorMolding()
    {
        Trim("Back Base Molding",new Vector3(0,.16f,28.74f),new Vector3(29.5f,.28f,.12f));
        Trim("Front Base Molding L",new Vector3(-8.4f,.16f,11.26f),new Vector3(12.8f,.28f,.12f));
        Trim("Front Base Molding R",new Vector3(8.4f,.16f,11.26f),new Vector3(12.8f,.28f,.12f));
        Trim("Left Base Molding",new Vector3(-14.74f,.16f,20),new Vector3(.12f,.28f,17.5f));
        Trim("Right Base Molding",new Vector3(14.74f,.16f,20),new Vector3(.12f,.28f,17.5f));
        Trim("West Divider Molding South",new Vector3(-6.82f,.16f,14.5f),new Vector3(.12f,.28f,7f));
        Trim("West Divider Molding North",new Vector3(-6.82f,.16f,25.5f),new Vector3(.12f,.28f,7f));
        Trim("Office Security Divider Molding",new Vector3(-11,.16f,20.14f),new Vector3(7.7f,.28f,.12f));
        Trim("Core Divider Molding",new Vector3(11.32f,.16f,19f),new Vector3(.12f,.28f,5.8f));
    }

    void Trim(string n,Vector3 p,Vector3 s){var g=Box(n,p,s,metal);g.GetComponent<Collider>().enabled=false;}

    void MakeSecondFloor()
    {
        const float y=7.9f;
        // 2F plan: three north rooms, central pursuit corridor, three south rooms, east stair core.
        Box("2F North Corridor Wall",new Vector3(-1.8f,y,22.2f),new Vector3(23.2f,5f,.22f),wall);
        Box("2F South Corridor Wall",new Vector3(-1.8f,y,18.4f),new Vector3(23.2f,5f,.22f),wall);
        Box("2F North Office Divider",new Vector3(-5.8f,y,25.6f),new Vector3(.22f,5f,6.8f),wall);
        Box("2F Meeting Divider",new Vector3(3.2f,y,25.6f),new Vector3(.22f,5f,6.8f),wall);
        Box("2F Specimen Divider",new Vector3(10.1f,y,25.6f),new Vector3(.22f,5f,6.8f),wall);
        Box("2F Cabinet Divider",new Vector3(-5.8f,y,14.7f),new Vector3(.22f,5f,6.6f),wall);
        Box("2F Archive Divider",new Vector3(3.2f,y,14.7f),new Vector3(.22f,5f,6.6f),wall);
        Box("2F Lounge Divider",new Vector3(10.1f,y,14.7f),new Vector3(.22f,5f,6.6f),wall);
        Box("2F Meeting Table",new Vector3(-1.2f,6.25f,25.5f),new Vector3(4.5f,.18f,1.6f),metal);
        Box("2F Archive Shelves",new Vector3(-1.2f,6.55f,15.2f),new Vector3(4.8f,2.1f,.55f),dark);
        Box("2F Lounge Sofa",new Vector3(6.3f,6.05f,15.2f),new Vector3(3.1f,1.2f,1.0f),dark);
        for(int i=0;i<7;i++) Box("2F Pursuit Ceiling Light",new Vector3(-10.5f+i*3.2f,10.15f,20.3f),new Vector3(1.4f,.08f,.28f),i%3==0?red:cyan);
        var stage3=new GameObject("Stage 3 Trigger"); stage3.transform.position=new Vector3(-13.7f,6.55f,20.3f); var c=stage3.AddComponent<BoxCollider>(); c.isTrigger=true;c.size=new Vector3(1.2f,2.4f,3.2f);stage3.AddComponent<Stage3Zone>();
    }

    void MakeThirdFloor()
    {
        const float y=13.1f;
        // 3F plan: development/USB north, server room center, control and ventilation south.
        Box("3F North Corridor Wall",new Vector3(0,y,24.4f),new Vector3(24f,5f,.22f),wall);
        Box("3F Server North Wall",new Vector3(0,y,21.8f),new Vector3(15f,5f,.22f),wall);
        Box("3F Server South Wall",new Vector3(0,y,17.5f),new Vector3(15f,5f,.22f),wall);
        Box("3F Server West Wall",new Vector3(-7.5f,y,19.65f),new Vector3(.22f,5f,4.5f),wall);
        Box("3F Server East Wall",new Vector3(7.5f,y,19.65f),new Vector3(.22f,5f,4.5f),wall);
        Box("3F Development Divider",new Vector3(-5.5f,y,26.7f),new Vector3(.22f,5f,4.6f),wall);
        Box("3F USB Divider",new Vector3(8.5f,y,26.7f),new Vector3(.22f,5f,4.6f),wall);
        Box("3F Control Divider",new Vector3(-5.5f,y,14.2f),new Vector3(.22f,5f,6.2f),wall);
        Box("3F Record Divider",new Vector3(3.5f,y,14.2f),new Vector3(.22f,5f,6.2f),wall);
        Box("3F Vent Divider",new Vector3(9.5f,y,14.2f),new Vector3(.22f,5f,6.2f),wall);
        for(int i=0;i<4;i++) Box("3F Server Rack",new Vector3(-5.4f+i*3.6f,11.75f,19.7f),new Vector3(1.2f,2.15f,2.8f),dark);
        Box("3F Development Desk",new Vector3(0,11.45f,27f),new Vector3(6f,.18f,1.2f),metal);
        Box("3F Control Console",new Vector3(-10.2f,11.55f,14.4f),new Vector3(5.2f,1.0f,1.1f),metal);
        var basement=new GameObject("Basement Trigger"); basement.transform.position=new Vector3(0,11.75f,11.7f); var c=basement.AddComponent<BoxCollider>();c.isTrigger=true;c.size=new Vector3(3,2.4f,1.1f);basement.AddComponent<BasementZone>();
    }

    void MakeBasement()
    {
        const float y=-3.25f;
        Box("B1 Floor",new Vector3(0,-5.85f,20),new Vector3(30,.25f,18),concrete);
        Box("B1 Ceiling",new Vector3(0,-.65f,20),new Vector3(30,.25f,18),dark);
        Box("B1 Back Wall",new Vector3(0,y,29),new Vector3(30,5.2f,.4f),concrete);
        Box("B1 Front Wall",new Vector3(0,y,11),new Vector3(30,5.2f,.4f),concrete);
        Box("B1 Left Wall",new Vector3(-15,y,20),new Vector3(.4f,5.2f,18),concrete);
        Box("B1 Right Wall",new Vector3(15,y,20),new Vector3(.4f,5.2f,18),concrete);
        Box("B1 Main Corridor North",new Vector3(0,y,21.3f),new Vector3(24f,5.2f,.22f),wall);
        Box("B1 Main Corridor South",new Vector3(0,y,17.7f),new Vector3(24f,5.2f,.22f),wall);
        foreach(float x in new[]{-10f,-4f,3f,9f}) Box("B1 Room Divider",new Vector3(x,y,25.1f),new Vector3(.22f,5.2f,7.6f),wall);
        foreach(float x in new[]{-7f,1f,8f}) Box("B1 Lab Divider",new Vector3(x,y,14.4f),new Vector3(.22f,5.2f,6.6f),wall);
        Box("B1 Sealed Door",new Vector3(-10.5f,-4.3f,17.7f),new Vector3(2.1f,3.1f,.18f),metal);
        Box("B1 Mutation Tank",new Vector3(11.5f,-4.0f,14.3f),new Vector3(3.2f,3.4f,3.2f),black);
        for(int i=0;i<6;i++) AddPoint(new Vector3(-11+i*4.4f,-1.2f,19.5f),i%2==0?new Color(.15f,.55f,.62f):new Color(.55f,.12f,.08f),.7f,4.2f);
    }

    void MakeAbandonedHospitalExterior()
    {
        // Two sealed upper storeys give the entrance the massing of an abandoned hospital.
        Box("2F Slab",new Vector3(0,5.35f,20),new Vector3(30,.28f,18),concrete);
        Box("3F Slab",new Vector3(0,10.55f,20),new Vector3(30,.28f,18),concrete);
        Box("Upper Front 2F",new Vector3(0,7.9f,11),new Vector3(30,5.1f,.5f),concrete);
        Box("Upper Front 3F",new Vector3(0,13.1f,11),new Vector3(30,5.1f,.5f),concrete);
        Box("Upper Back 2F",new Vector3(0,7.9f,29),new Vector3(30,5.1f,.5f),concrete);
        Box("Upper Back 3F",new Vector3(0,13.1f,29),new Vector3(30,5.1f,.5f),concrete);
        Box("Upper Left 2F",new Vector3(-15,7.9f,20),new Vector3(.5f,5.1f,18),concrete);
        Box("Upper Left 3F",new Vector3(-15,13.1f,20),new Vector3(.5f,5.1f,18),concrete);
        Box("Upper Right 2F",new Vector3(15,7.9f,20),new Vector3(.5f,5.1f,18),concrete);
        Box("Upper Right 3F",new Vector3(15,13.1f,20),new Vector3(.5f,5.1f,18),concrete);
        Box("Hospital Roof",new Vector3(0,15.72f,20),new Vector3(30.7f,.35f,18.7f),dark);
        Box("Roof Parapet Front",new Vector3(0,16.25f,10.72f),new Vector3(31,.9f,.35f),concrete);
        Box("Roof Parapet Back",new Vector3(0,16.25f,29.28f),new Vector3(31,.9f,.35f),concrete);
        Box("Roof Parapet Left",new Vector3(-15.28f,16.25f,20),new Vector3(.35f,.9f,18.8f),concrete);
        Box("Roof Parapet Right",new Vector3(15.28f,16.25f,20),new Vector3(.35f,.9f,18.8f),concrete);

        // Deep concrete canopy and stair tower match the supplied references.
        Box("Entrance Canopy",new Vector3(0,5.05f,8.9f),new Vector3(7.2f,.4f,4.2f),concrete);
        Box("Canopy Fascia",new Vector3(0,4.8f,6.82f),new Vector3(7.35f,.65f,.3f),metal);
        Box("Stair Tower Inner Pier",new Vector3(9.9f,11.2f,20),new Vector3(.35f,11.2f,17.2f),concrete);
        Box("Stair Tower Front Pier",new Vector3(12.4f,11.2f,11.45f),new Vector3(5.2f,11.2f,.35f),concrete);
        Box("Stair Tower Back Pier",new Vector3(12.4f,11.2f,28.55f),new Vector3(5.2f,11.2f,.35f),concrete);
        Box("Stair Tower Cap",new Vector3(12.4f,17.05f,20),new Vector3(5.7f,.5f,17.7f),dark);

        // Front window rows. Each visual window has its own invisible solid blocker.
        foreach(float y in new[]{7.8f,13f})
            foreach(float x in new[]{-11.8f,-8.3f,-4.8f,4.8f,8.3f})
                MakeBlockedWindow("Front",new Vector3(x,y,10.72f),Quaternion.identity);
        foreach(float y in new[]{2.7f,7.8f,13f})
            foreach(float z in new[]{14.2f,18.1f,22f,25.9f})
            {
                if (!(y == 2.7f && z == 25.9f))
                    MakeBlockedWindow("Left",new Vector3(-15.28f,y,z),Quaternion.Euler(0,90,0));
                MakeBlockedWindow("Right",new Vector3(15.28f,y,z),Quaternion.Euler(0,90,0));
            }
        // First-floor facade windows between the door and fan bay.
        foreach(float x in new[]{-10.5f,-6.3f,5.3f}) MakeBlockedWindow("Ground",new Vector3(x,2.7f,10.7f),Quaternion.identity);

        // Grime bands break up the clean procedural walls without extra lights.
        Box("Concrete Grime Band 2F",new Vector3(0,5.75f,10.43f),new Vector3(30,.3f,.08f),dark);
        Box("Concrete Grime Band 3F",new Vector3(0,10.95f,10.43f),new Vector3(30,.3f,.08f),dark);
    }

    void MakeBlockedWindow(string prefix,Vector3 p,Quaternion rotation)
    {
        var pane=Box(prefix+" Window Glass",p,new Vector3(2.25f,1.65f,.12f),black); pane.transform.rotation=rotation;
        var top=Box(prefix+" Window Frame",p+rotation*new Vector3(0,.94f,0),new Vector3(2.65f,.18f,.3f),metal); top.transform.rotation=rotation;
        var bottom=Box(prefix+" Window Frame",p+rotation*new Vector3(0,-.94f,0),new Vector3(2.65f,.18f,.3f),metal); bottom.transform.rotation=rotation;
        var left=Box(prefix+" Window Frame",p+rotation*new Vector3(-1.24f,0,0),new Vector3(.18f,1.72f,.3f),metal); left.transform.rotation=rotation;
        var right=Box(prefix+" Window Frame",p+rotation*new Vector3(1.24f,0,0),new Vector3(.18f,1.72f,.3f),metal); right.transform.rotation=rotation;
        var blocker=Box(prefix+" Window Blocker",p+rotation*new Vector3(0,0,.08f),new Vector3(2.45f,1.9f,.4f),black); blocker.transform.rotation=rotation; blocker.GetComponent<Renderer>().enabled=false;
    }

    void MakeSecurityVaultWindow()
    {
        // The only vaultable window is on the guard room exterior wall.
        Vector3 p=new Vector3(-15.28f,2.35f,25.9f); Quaternion q=Quaternion.Euler(0,90,0);
        var opening=Box("Security Vault Window",p,new Vector3(2.35f,1.75f,.08f),black); opening.transform.rotation=q; opening.GetComponent<Collider>().enabled=false;
        var top=Box("Security Window Frame",p+Vector3.up*.98f,new Vector3(2.7f,.18f,.3f),metal); top.transform.rotation=q;
        var bottom=Box("Security Window Frame",p+Vector3.down*.98f,new Vector3(2.7f,.18f,.3f),metal); bottom.transform.rotation=q;
        var a=Box("Security Window Frame",p+Vector3.forward*1.26f,new Vector3(.18f,1.82f,.3f),metal); a.transform.rotation=q;
        var b=Box("Security Window Frame",p+Vector3.back*1.26f,new Vector3(.18f,1.82f,.3f),metal); b.transform.rotation=q;
        var trigger=new GameObject("Security Window Vault Trigger"); trigger.transform.position=p; var col=trigger.AddComponent<BoxCollider>(); col.isTrigger=true; col.size=new Vector3(3.4f,2.8f,2.7f);
        var vault=trigger.AddComponent<VaultWindow>(); vault.outsidePoint=new Vector3(-17.05f,1.1f,25.9f); vault.insidePoint=new Vector3(-13.55f,1.1f,25.9f);
    }

    void MakeBlueprintFacility()
    {
        // 1F plan: Entrance/Lobby - Generator - Distribution - Storage - Corridor,
        // with Security and Stairs on the south side.
        Box("1F Floor",new Vector3(0,.06f,20),new Vector3(28,.12f,16),concrete);
        Box("1F Roof",new Vector3(0,5.25f,20),new Vector3(28.5f,.3f,16.5f),dark);
        Box("Exterior Back",new Vector3(0,2.6f,28),new Vector3(28,5.2f,.45f),concrete);
        Box("Exterior Left",new Vector3(-14,2.6f,20),new Vector3(.45f,5.2f,16),concrete);
        Box("Exterior Right",new Vector3(14,2.6f,20),new Vector3(.45f,5.2f,16),concrete);
        Box("Front Wall Short",new Vector3(-13.2f,2.6f,12),new Vector3(1.6f,5.2f,.5f),concrete);
        Box("Front Wall Main",new Vector3(1.25f,2.6f,12),new Vector3(20.5f,5.2f,.5f),concrete);
        Box("Front Wall Right",new Vector3(12.9f,2.6f,12),new Vector3(2.2f,5.2f,.5f),concrete);
        Box("Facade Band",new Vector3(0,4.85f,11.78f),new Vector3(28.5f,.7f,.72f),metal);

        // Main entrance at the left, matching the supplied 1F plan.
        Vector3 doorCenter=new Vector3(-11,2.05f,11.72f);
        Box("Door Frame Left",doorCenter+new Vector3(-2.05f,.1f,.05f),new Vector3(.35f,4.4f,.75f),metal);
        Box("Door Frame Right",doorCenter+new Vector3(2.05f,.1f,.05f),new Vector3(.35f,4.4f,.75f),metal);
        Box("Door Frame Lintel",doorCenter+new Vector3(0,2.2f,.05f),new Vector3(4.4f,.35f,.75f),metal);
        var leftDoor=Box("Entrance Door Leaf L",doorCenter+new Vector3(-.92f,0,-.08f),new Vector3(1.76f,3.85f,.18f),metal);
        var rightDoor=Box("Entrance Door Leaf R",doorCenter+new Vector3(.92f,0,-.08f),new Vector3(1.76f,3.85f,.18f),metal);
        entrance.Add(leftDoor); entrance.Add(rightDoor);
        var blocker=Box("Entrance Door Blocker",doorCenter,new Vector3(3.65f,3.9f,.12f),metal); blocker.GetComponent<Renderer>().enabled=false; entrance.Add(blocker);
        Box("Entrance Status",doorCenter+new Vector3(0,1.62f,-.24f),new Vector3(1.2f,.08f,.06f),red);
        var entryLamp=Box("Entrance Lamp",new Vector3(-11,4.55f,12.25f),new Vector3(1.7f,.1f,.2f),cyan); AddPoint(entryLamp.transform.position,Color.cyan,1.5f,5.5f);

        // Internal plan walls. Door gaps are left between the segments.
        Box("Lobby East Wall A",new Vector3(-8.2f,2.55f,14.7f),new Vector3(.25f,5.1f,5.4f),wall);
        Box("Lobby East Wall B",new Vector3(-8.2f,2.55f,21.8f),new Vector3(.25f,5.1f,4.4f),wall);
        Box("Generator East Wall A",new Vector3(-2.5f,2.55f,17),new Vector3(.25f,5.1f,10),wall);
        Box("Generator East Wall B",new Vector3(-2.5f,2.55f,26.6f),new Vector3(.25f,5.1f,2.8f),wall);
        Box("Distribution East Wall A",new Vector3(3.5f,2.55f,17),new Vector3(.25f,5.1f,10),wall);
        Box("Distribution East Wall B",new Vector3(3.5f,2.55f,26.6f),new Vector3(.25f,5.1f,2.8f),wall);
        Box("Storage East Wall A",new Vector3(9,2.55f,17),new Vector3(.25f,5.1f,10),wall);
        Box("Storage East Wall B",new Vector3(9,2.55f,26.6f),new Vector3(.25f,5.1f,2.8f),wall);
        Box("North Corridor Divider L",new Vector3(-5.4f,2.55f,22.9f),new Vector3(5.6f,5.1f,.25f),wall);
        Box("North Corridor Divider M",new Vector3(.5f,2.55f,22.9f),new Vector3(3.5f,5.1f,.25f),wall);
        Box("North Corridor Divider R",new Vector3(6.2f,2.55f,22.9f),new Vector3(3.2f,5.1f,.25f),wall);
        Box("Security North Wall L",new Vector3(-11.6f,2.55f,17.2f),new Vector3(4.8f,5.1f,.25f),wall);
        Box("Security North Wall R",new Vector3(-8.9f,2.55f,17.2f),new Vector3(.9f,5.1f,.25f),wall);
        Box("Stair West Wall",new Vector3(-2.4f,2.55f,14.6f),new Vector3(.25f,5.1f,5.2f),wall);
        Box("Stair East Wall",new Vector3(2.4f,2.55f,14.6f),new Vector3(.25f,5.1f,5.2f),wall);

        // Readable room landmarks from the plan.
        Cylinder("Generator Core",new Vector3(-5.3f,1.25f,20.2f),new Vector3(1.45f,1.25f,1.45f),metal,Quaternion.identity);
        for(int i=0;i<3;i++) Box("Distribution Cabinet",new Vector3(-.5f+i*1.35f,1.65f,26.8f),new Vector3(1.05f,3.3f,.55f),metal);
        for(int i=0;i<3;i++) Box("Storage Rack",new Vector3(5.1f+i*1.25f,1.5f,26.6f),new Vector3(.9f,3f,.7f),dark);
        for(int i=0;i<5;i++) Box("Stair Step",new Vector3(0,.2f+i*.32f,13.1f+i*.55f),new Vector3(4.2f,.32f,.6f),metal);
        AddPoint(new Vector3(-5.3f,4.1f,20.2f),new Color(.85f,.55f,.25f),1.2f,6);
        AddPoint(new Vector3(.5f,4.1f,25.5f),new Color(.15f,.6f,.9f),1.1f,6);
        AddPoint(new Vector3(11.5f,4.1f,21),new Color(.15f,.6f,.9f),.9f,7);

        // Security room gameplay objects.
        Vector3 security=new Vector3(-10.7f,0,14.5f);
        Box("Desk",security+new Vector3(0,1.1f,1.4f),new Vector3(4.2f,.18f,1.15f),metal);
        Box("CRT",security+new Vector3(-1.1f,1.7f,1.45f),new Vector3(1.1f,.8f,.7f),black);
        var card=Box("Access Card",security+new Vector3(.8f,1.35f,1.2f),new Vector3(.24f,.025f,.15f),cyan); card.isStatic=false; card.AddComponent<AccessCard>();
        var map=Box("1F Laboratory Map",new Vector3(-13.72f,2.7f,14.5f),new Vector3(.08f,2.25f,3.5f),black); map.AddComponent<MapPanel>();
        var securityDoor=Box("Security Door",new Vector3(-8.2f,1.5f,19.2f),new Vector3(.18f,3,1.8f),metal); securityDoor.isStatic=false; securityDoor.AddComponent<SecurityDoor>();

        // Only the door breaks: small door fragments, never facade or ceiling pieces.
        for(int i=0;i<12;i++)
        {
            float x=-11+(i%4-1.5f)*.55f, y=1.05f+(i/4)*.7f, z=11.72f+(i%2)*.14f;
            var chunk=Box("Door Debris",new Vector3(x,y,z),new Vector3(.48f,.62f,.14f),i%3==0?dark:metal);
            chunk.isStatic=false; chunk.transform.rotation=Quaternion.Euler(i*13%28,i*31%55,i*19%24);
            var body=chunk.AddComponent<Rigidbody>(); body.mass=2.5f+i*.18f; body.linearDamping=.35f; body.angularDamping=.45f;
            chunk.SetActive(false); rubble.Add(chunk);
        }
        var dustGo=new GameObject("Collapse Dust"); dustGo.transform.position=new Vector3(-11,1.8f,11.7f); collapseDust=dustGo.AddComponent<ParticleSystem>(); var dm=collapseDust.main; dm.playOnAwake=false; dm.duration=.7f; dm.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.85f); dm.startSpeed=new ParticleSystem.MinMaxCurve(.35f,1.3f); dm.startSize=new ParticleSystem.MinMaxCurve(.025f,.1f); dm.maxParticles=65; dm.startColor=new Color(.3f,.32f,.34f,.48f); var de=collapseDust.emission; de.rateOverTime=0; de.SetBursts(new[]{new ParticleSystem.Burst(0,48)});
        var zone=new GameObject("Collapse Trigger"); zone.transform.position=new Vector3(-11,1.2f,9.8f); var col=zone.AddComponent<BoxCollider>(); col.isTrigger=true; col.size=new Vector3(3.8f,3,1.2f); zone.AddComponent<CollapseZone>();
        var stage=new GameObject("Stage 2 Trigger"); stage.transform.position=new Vector3(0,1.2f,16); var sc=stage.AddComponent<BoxCollider>(); sc.isTrigger=true; sc.size=new Vector3(4,2.4f,1); stage.AddComponent<Stage2Zone>();

        // Exterior details from the supplied mood reference.
        for(int y=0;y<2;y++){ Cylinder("Facade Fan",new Vector3(7.5f,2f+y*2.1f,11.68f),new Vector3(1.25f,.15f,1.25f),metal,Quaternion.Euler(90,0,0)); for(int i=-2;i<=2;i++) Box("Fan Slat",new Vector3(7.5f,2f+y*2.1f+i*.22f,11.48f),new Vector3(2.4f,.07f,.08f),dark); }
        Box("Approach Road",new Vector3(-11,.015f,-1),new Vector3(6,.03f,26),black);
        InvisibleBlock("Boundary Left",new Vector3(-20,2,5),new Vector3(.4f,4,38)); InvisibleBlock("Boundary Right",new Vector3(20,2,5),new Vector3(.4f,4,38)); InvisibleBlock("Boundary Back",new Vector3(0,2,-14),new Vector3(40,4,.4f));
    }

void MakeFacility()
    {
        // Blueprint: a symmetrical laboratory shell with a protected central entry bay.
        Box("Main Building Floor",new Vector3(0,.08f,18),new Vector3(24,.16f,10),concrete);
        Box("Facility Back",new Vector3(0,4,23),new Vector3(24,8,.45f),concrete);
        Box("Main Exterior Left",new Vector3(-12,4,18),new Vector3(.45f,8,10),concrete);
        Box("Main Exterior Right",new Vector3(12,4,18),new Vector3(.45f,8,10),concrete);
        Box("Main Building Roof",new Vector3(0,8.05f,18),new Vector3(24.5f,.3f,10.5f),dark);
        Box("Facade Left Wing",new Vector3(-7.1f,3.7f,13),new Vector3(9.8f,7.4f,.5f),concrete);
        Box("Facade Right Wing",new Vector3(7.1f,3.7f,13),new Vector3(9.8f,7.4f,.5f),concrete);
        Box("Facade Header",new Vector3(0,6.15f,13),new Vector3(4.4f,2.5f,.55f),concrete);
        Box("Door Frame Left",new Vector3(-2.05f,2.2f,12.7f),new Vector3(.35f,4.4f,.75f),metal);
        Box("Door Frame Right",new Vector3(2.05f,2.2f,12.7f),new Vector3(.35f,4.4f,.75f),metal);
        Box("Door Frame Lintel",new Vector3(0,4.25f,12.7f),new Vector3(4.4f,.35f,.75f),metal);
        Box("Facade Upper Band",new Vector3(0,7.55f,12.9f),new Vector3(24,1.1f,.7f),concrete);
        Box("Roof Parapet Front",new Vector3(0,8.62f,12.78f),new Vector3(24.6f,.9f,.35f),metal);
        Box("Roof Parapet Back",new Vector3(0,8.62f,23.22f),new Vector3(24.6f,.9f,.35f),metal);
        Box("Entry Floor",new Vector3(0,.06f,18),new Vector3(4,.12f,10),metal);
        Box("Entry Left Wall",new Vector3(-2.1f,2.1f,18),new Vector3(.2f,4.2f,10),wall);
        Box("Entry Right Wall",new Vector3(2.1f,2.1f,18),new Vector3(.2f,4.2f,10),wall);
        Box("Entry Ceiling",new Vector3(0,4.2f,18),new Vector3(4.2f,.2f,10),dark);
        for(int side=-1;side<=1;side+=2) for(int i=0;i<3;i++)
        {
            float x=side*(4.2f+i*2.45f);
            Box("Facade Window Frame",new Vector3(x,3.7f,12.7f),new Vector3(2.05f,2.65f,.25f),metal);
            Box("Facade Window Glass",new Vector3(x,3.7f,12.54f),new Vector3(1.72f,2.32f,.08f),black);
            Box("Window Glow Strip",new Vector3(x,2.65f,12.48f),new Vector3(1.45f,.05f,.04f),cyan);
        }
        var leftDoor=Box("Entrance Door Leaf L",new Vector3(-.92f,2.02f,12.52f),new Vector3(1.76f,3.85f,.18f),metal);
        var rightDoor=Box("Entrance Door Leaf R",new Vector3(.92f,2.02f,12.52f),new Vector3(1.76f,3.85f,.18f),metal);
        entrance.Add(leftDoor); entrance.Add(rightDoor);
        var blocker=Box("Entrance Door Blocker",new Vector3(0,1.95f,12.7f),new Vector3(3.65f,3.9f,.12f),metal); blocker.GetComponent<Renderer>().enabled=false; entrance.Add(blocker);
        Box("Entrance Door Visual",new Vector3(0,3.55f,12.4f),new Vector3(1.1f,.08f,.08f),red);
        var lamp=Box("Entrance Lamp",new Vector3(0,4.7f,13.15f),new Vector3(1.8f,.1f,.22f),cyan); AddPoint(lamp.transform.position,Color.cyan,1.6f,6);
        for(int i=0;i<12;i++)
        {
            float x=(i%4-1.5f)*.55f, y=1.1f+(i/4)*.7f, z=12.45f+(i%2)*.16f;
            var chunk=Box("Door Debris",new Vector3(x,y,z),new Vector3(.48f,.62f,.14f),i%3==0?dark:metal);
            chunk.isStatic=false; chunk.transform.rotation=Quaternion.Euler(i*13%28,i*31%55,i*19%24);
            var body=chunk.AddComponent<Rigidbody>(); body.mass=2.5f+i*.18f; body.linearDamping=.35f; body.angularDamping=.45f;
            chunk.SetActive(false); rubble.Add(chunk);
        }
        var dustGo=new GameObject("Collapse Dust"); dustGo.transform.position=new Vector3(0,1.8f,13.5f); collapseDust=dustGo.AddComponent<ParticleSystem>(); var dm=collapseDust.main; dm.playOnAwake=false; dm.duration=.8f; dm.startLifetime=new ParticleSystem.MinMaxCurve(.45f,1.1f); dm.startSpeed=new ParticleSystem.MinMaxCurve(.5f,2.1f); dm.startSize=new ParticleSystem.MinMaxCurve(.035f,.14f); dm.maxParticles=100; dm.startColor=new Color(.38f,.4f,.42f,.55f); var de=collapseDust.emission; de.rateOverTime=0; de.SetBursts(new[]{new ParticleSystem.Burst(0,75)}); var ds=collapseDust.shape; ds.shapeType=ParticleSystemShapeType.Box; ds.scale=new Vector3(3.2f,2.4f,.45f); var dr=collapseDust.GetComponent<ParticleSystemRenderer>(); var dustShader=Shader.Find("Universal Render Pipeline/Particles/Unlit"); if(dustShader!=null){var dustMaterial=new Material(dustShader);dustMaterial.SetColor("_BaseColor",new Color(.38f,.4f,.42f,.55f));dr.material=dustMaterial;}
        var zone=new GameObject("Collapse Trigger"); zone.transform.position=new Vector3(0,1.2f,10.4f); var col=zone.AddComponent<BoxCollider>(); col.isTrigger=true; col.size=new Vector3(3.8f,3,1.2f); zone.AddComponent<CollapseZone>();
        var warning=Box("Blood Warning",new Vector3(4.25f,1.85f,6.72f),new Vector3(.85f,1.1f,.035f),paper); warning.AddComponent<StoryNote>();
        Box("Blood Mark",new Vector3(4.25f,1.8f,6.68f),new Vector3(.14f,.52f,.025f),red).AddComponent<StoryNote>();
        Box("Approach Road",new Vector3(0,.015f,-2),new Vector3(6,.03f,30),black);
        Box("Curb Left",new Vector3(-3.25f,.18f,-2),new Vector3(.35f,.35f,30),concrete);
        Box("Curb Right",new Vector3(3.25f,.18f,-2),new Vector3(.35f,.35f,30),concrete);
        InvisibleBlock("Boundary Left",new Vector3(-18,2,4),new Vector3(.4f,4,34)); InvisibleBlock("Boundary Right",new Vector3(18,2,4),new Vector3(.4f,4,34)); InvisibleBlock("Boundary Back",new Vector3(0,2,-18),new Vector3(36,4,.4f));
    }

    void MakeBooth()
    {
        // Exterior guard room physically attached to the right side of the main facade.
        var c=new Vector3(6,0,10); Box("Booth Floor",c+new Vector3(0,.05f,0),new Vector3(6,.1f,6),concrete);
        Box("Booth Left",c+new Vector3(-3,2.5f,0),new Vector3(.24f,5,6),wall); Box("Booth Right",c+new Vector3(3,2.5f,0),new Vector3(.24f,5,6),wall);
        Box("Booth Front L",c+new Vector3(-2.15f,2.5f,-3),new Vector3(1.7f,5,.24f),wall); Box("Booth Front R",c+new Vector3(2.15f,2.5f,-3),new Vector3(1.7f,5,.24f),wall); Box("Booth Front Header",c+new Vector3(0,4.35f,-3),new Vector3(2.6f,1.3f,.24f),wall);
        Box("Booth Back L",c+new Vector3(-2.05f,2.5f,3),new Vector3(1.9f,5,.24f),wall); Box("Booth Back R",c+new Vector3(2.05f,2.5f,3),new Vector3(1.9f,5,.24f),wall); Box("Booth Back Header",c+new Vector3(0,4.3f,3),new Vector3(2.2f,1.4f,.24f),wall);
        Box("Booth Roof",c+new Vector3(0,5,0),new Vector3(6,.24f,6),concrete);
        Box("Booth Roof Fascia Front",c+new Vector3(0,5.25f,-3.02f),new Vector3(6.25f,.55f,.3f),metal);
        Box("Booth Roof Fascia Left",c+new Vector3(-3.02f,5.25f,0),new Vector3(.3f,.55f,6.25f),metal);
        Box("Booth Roof Fascia Right",c+new Vector3(3.02f,5.25f,0),new Vector3(.3f,.55f,6.25f),metal);
        Box("Booth Canopy",c+new Vector3(0,3.75f,-3.55f),new Vector3(3.2f,.18f,1.15f),metal);
        Box("Booth Connector Roof",new Vector3(6,5,13.5f),new Vector3(6,.24f,1),concrete);
        Box("Booth Connector Left",new Vector3(3,2.5f,13.5f),new Vector3(.24f,5,1),wall);
        Box("Booth Connector Right",new Vector3(9,2.5f,13.5f),new Vector3(.24f,5,1),wall);
        Box("Desk",c+new Vector3(0,1.25f,1.9f),new Vector3(4.2f,.18f,1.2f),metal); Box("Desk L",c+new Vector3(-1.8f,.62f,1.9f),new Vector3(.25f,1.25f,1),metal); Box("Desk R",c+new Vector3(1.8f,.62f,1.9f),new Vector3(.25f,1.25f,1),metal); Box("CRT",c+new Vector3(-1.1f,1.75f,2),new Vector3(1.15f,.85f,.75f),black); Box("Document",c+new Vector3(1.3f,1.4f,1.6f),new Vector3(.7f,.02f,.5f),paper);
        var card=Box("Access Card",c+new Vector3(.75f,1.72f,1.35f),new Vector3(.24f,.025f,.15f),cyan); card.isStatic=false; card.AddComponent<AccessCard>(); AddPoint(c+new Vector3(.75f,1.95f,1.35f),new Color(.1f,.8f,1),.4f,1.2f);
        var map=Box("1F Laboratory Map",c+new Vector3(0,3f,2.82f),new Vector3(3.35f,2.15f,.09f),black); map.AddComponent<MapPanel>(); AddPoint(c+new Vector3(1.5f,2.4f,1.3f),new Color(1,.65f,.32f),1.3f,6);
        var securityDoor=Box("Security Door",c+new Vector3(0,1.5f,3.05f),new Vector3(1.8f,3,.18f),metal); securityDoor.isStatic=false; securityDoor.AddComponent<SecurityDoor>();
        MakeSecretCorridor();
    }

    void MakeSecretCorridor()
    {
        // Narrow service corridor runs directly from the guard room into the main building.
        Box("Secret Corridor Floor",new Vector3(6,.06f,16.5f),new Vector3(2.5f,.12f,7),metal); Box("Secret Corridor Left",new Vector3(4.75f,2.1f,16.5f),new Vector3(.22f,4.2f,7),concrete); Box("Secret Corridor Right",new Vector3(7.25f,2.1f,16.5f),new Vector3(.22f,4.2f,7),concrete); Box("Secret Corridor Ceiling",new Vector3(6,4.15f,16.5f),new Vector3(2.5f,.22f,7),dark);
        AddPoint(new Vector3(6,3.35f,15.2f),new Color(.12f,.55f,.7f),1.2f,4); AddPoint(new Vector3(6,3.1f,18.7f),new Color(.8f,.04f,.02f),1f,3.5f);
        var trigger=new GameObject("Stage 2 Trigger"); trigger.transform.position=new Vector3(6,1.2f,19.3f); var col=trigger.AddComponent<BoxCollider>(); col.isTrigger=true; col.size=new Vector3(2.1f,2.4f,1); trigger.AddComponent<Stage2Zone>();
    }

    void MakePlayer(){ var go=new GameObject("Player"); go.tag="Player"; go.transform.position=new Vector3(0,1.1f,-13); player=go.AddComponent<CharacterController>(); player.height=1.8f; player.radius=.35f; player.center=new Vector3(0,.9f,0); var cg=new GameObject("Main Camera"); cg.tag="MainCamera"; cg.transform.SetParent(go.transform); cg.transform.localPosition=new Vector3(0,1.55f,0); cam=cg.AddComponent<Camera>(); cam.fieldOfView=66; cam.nearClipPlane=.05f; cam.farClipPlane=70; cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.008f,.012f,.018f); cg.AddComponent<AudioListener>(); flashlight=cg.AddComponent<Light>(); flashlight.type=LightType.Spot; flashlight.range=18; flashlight.spotAngle=48; flashlight.intensity=3.2f; flashlight.shadows=LightShadows.None; flashlight.color=new Color(.82f,.9f,1); }
    void MakeRain(){ var go=new GameObject("Rain"); go.transform.position=new Vector3(0,12,4); var ps=go.AddComponent<ParticleSystem>(); var main=ps.main; main.startLifetime=1.15f; main.startSpeed=18; main.startSize=.022f; main.maxParticles=360; main.simulationSpace=ParticleSystemSimulationSpace.World; var emission=ps.emission; emission.rateOverTime=180; var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(28,1,34); go.transform.rotation=Quaternion.Euler(90,0,0); }

    void Update()
    {
        var keyboard=Keyboard.current; var mouse=Mouse.current; if(keyboard==null||mouse==null)return; if((mapOpen||storyOpen)){if(keyboard.eKey.wasPressedThisFrame||keyboard.escapeKey.wasPressedThisFrame){if(mapOpen)CloseMap();else{storyOpen=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}} return;}
        Vector2 look=mouse.delta.ReadValue()*.08f; player.transform.Rotate(0,look.x,0); pitch=Mathf.Clamp(pitch-look.y,-80,80); cam.transform.localRotation=Quaternion.Euler(pitch,0,0); float h=(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0); float v=(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0); Vector3 move=player.transform.right*h+player.transform.forward*v; if(move.sqrMagnitude>1)move.Normalize(); player.Move((move*(keyboard.leftShiftKey.isPressed?5f:3f)+Vector3.down*3f)*Time.deltaTime); if(!collapsed&&player.transform.position.z>10.1f&&Mathf.Abs(player.transform.position.x)<2.1f)TriggerCollapse(); if(keyboard.fKey.wasPressedThisFrame)flashlight.enabled=!flashlight.enabled; if(keyboard.eKey.wasPressedThisFrame)Interact();
        Vector3 pp=player.transform.position;
        if(!stage2Reached&&pp.y<5f&&pp.x>11.5f&&pp.z>24.5f)ReachStage2();
        else if(pp.y>5.5f&&pp.y<10.5f&&pp.x<-13f&&Mathf.Abs(pp.z-20.3f)<2f)ReachStage3();
        else if(pp.y>10.5f&&Mathf.Abs(pp.x)<1.8f&&pp.z<12.4f)ReachBasement();
    }
    void Interact(){if(Physics.SphereCast(cam.transform.position,.09f,cam.transform.forward,out var h,3.5f))h.collider.GetComponentInParent<IInteractable>()?.Use();}
    public void TriggerCollapse(){if(!collapsed){collapsed=true;StartCoroutine(Collapse());}}
    IEnumerator Collapse(){objective="피 묻은 경고문과 경비실을 조사하세요"; Show("콰앙! 정문이 무너져 돌아갈 수 없다.",3); if(collapseDust!=null)collapseDust.Play(); var p=cam.transform.localPosition; for(float t=0;t<1.05f;t+=Time.deltaTime){cam.transform.localPosition=p+Random.insideUnitSphere*(.035f+Mathf.Sin(t*34)*.025f);yield return null;} cam.transform.localPosition=p; foreach(var n in entrance)n.SetActive(false); if(collapsedEntranceBlocker!=null)collapsedEntranceBlocker.SetActive(true); if(brokenEntranceDoor!=null){brokenEntranceDoor.SetActive(true); var bb=brokenEntranceDoor.GetComponent<Rigidbody>(); if(bb!=null){bb.linearVelocity=Vector3.zero; bb.angularVelocity=Vector3.zero; bb.AddForce(new Vector3(Random.Range(-1.5f,1.5f),-0.8f,1.6f),ForceMode.Impulse); bb.AddTorque(new Vector3(Random.Range(-2f,2f),Random.Range(-1f,1f),Random.Range(2.5f,4.5f)),ForceMode.Impulse);}} foreach(var r in rubble)r.SetActive(true);}
    public void TakeCard(GameObject go){hasCard=true;Destroy(go);objective="벽의 1층 안내도를 확인하세요";Show("경비실 출입증을 획득했다.",3);}
    public void OpenMap(){mapOpen=true;if(!readMap){readMap=true;objective="경비실 뒤편의 숨겨진 보안문을 찾으세요";Show("지도에는 없는 연결 통로... 본관으로 이어지는 것 같다.",4);}Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    public void OpenStory(){storyOpen=true;objective="경비실에서 출입증과 1층 안내도를 찾으세요";Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    void CloseMap(){mapOpen=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;Show("지도에 표시되지 않은 통로가 보안문 뒤에 있다.",3);}
    public void TryDoor(GameObject go){if(doorOpened)return;if(!hasCard){Show("경비실 출입증이 필요하다.",2);return;}if(!readMap){Show("먼저 1층 안내도에서 숨은 통로 위치를 확인하자.",2);return;}doorOpened=true;go.transform.position+=Vector3.up*3.2f;if(securityDoorVisual!=null&&securityDoorVisual!=go)securityDoorVisual.transform.position+=Vector3.up*3.2f;objective="숨겨진 연결 통로를 따라 본관으로 들어가세요";Show("출입증 승인. 본관 비밀 출입구가 열렸다.",3);}
    public void TrySecretWall(GameObject go){if(!doorOpened){Show("반대편 보안문부터 열어야 한다.",2);return;}if(secretOpened)return;secretOpened=true;StartCoroutine(OpenSecret(go));}
    IEnumerator OpenSecret(GameObject go){objective="비밀통로를 따라 다음 구역으로 이동하세요";Show("벽 안쪽에서 기계음이 들린다...",2);Vector3 start=go.transform.position,end=start+Vector3.up*4.2f;for(float t=0;t<1.5f;t+=Time.deltaTime){go.transform.position=Vector3.Lerp(start,end,t/1.5f);yield return null;}go.transform.position=end;Show("숨겨진 통로가 열렸다.",3);}
    void MovePlayer(Vector3 target){player.enabled=false;player.transform.position=target;player.enabled=true;}
    public void ReachStage2(){if(stage2Reached)return;stage2Reached=true;objective="1층 구조 확인 완료";Show("CHECKPOINT — 1층 블록아웃 종료",5);}
    public void ReachStage3(){MovePlayer(new Vector3(0,11.65f,27.2f));objective="3층: 개발실과 서버실에서 기록을 찾으세요";Show("CHECKPOINT — 연구소 3층",5);}
    public void ReachBasement(){MovePlayer(new Vector3(-12.5f,-4.75f,19.5f));objective="지하: 봉쇄문 너머 생체실험 구역을 조사하세요";Show("CHECKPOINT — 지하 연구구역",5);}
    void Show(string s,float sec){message=s;messageUntil=Time.time+sec;}

    void OnGUI()
    {
        var title=new GUIStyle(GUI.skin.label){fontSize=20};title.normal.textColor=Color.white;GUI.Label(new Rect(22,18,900,32),"목표: "+objective,title);GUI.Label(new Rect(22,Screen.height-38,800,28),"WASD 이동 | 마우스 시점 | E 조사 | F 손전등 | Shift 달리기 | Space 창문 넘기");GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-12,20,24),"+",title);if(Time.time<messageUntil)GUI.Box(new Rect(Screen.width/2-240,Screen.height-110,480,45),message);
        if(cam!=null&&Physics.SphereCast(cam.transform.position,.09f,cam.transform.forward,out var hit,3.5f)){var target=hit.collider.gameObject;string prompt=target.GetComponentInParent<AccessCard>()!=null?"E  출입증 획득":target.GetComponentInParent<MapPanel>()!=null?"E  1층 안내도 확인":target.GetComponentInParent<StoryNote>()!=null?"E  피 묻은 경고문 읽기":target.GetComponentInParent<SecurityDoor>()!=null?"E  보안문 열기":target.GetComponentInParent<SecretWall>()!=null?"E  수상한 벽 조사":"";if(prompt.Length>0)GUI.Label(new Rect(Screen.width/2-90,Screen.height/2+35,220,32),prompt,title);}
        if(storyOpen){GUI.Box(new Rect(Screen.width*.2f,Screen.height*.2f,Screen.width*.6f,Screen.height*.55f),"찢어진 대외비 문서");var body=new GUIStyle(GUI.skin.label){fontSize=23,wordWrap=true};body.normal.textColor=new Color(.82f,.82f,.78f);GUI.Label(new Rect(Screen.width*.25f,Screen.height*.3f,Screen.width*.5f,Screen.height*.3f),"지하 대외비 생체실험실...?\n단순 주파수 연구소 아니었나?\n\n[문서 하단은 피에 젖어 읽을 수 없다]",body);GUI.Label(new Rect(Screen.width*.38f,Screen.height*.68f,400,30),"E 또는 ESC: 문서 닫기",title);return;}
        if(!mapOpen)return;GUI.Box(new Rect(Screen.width*.18f,Screen.height*.12f,Screen.width*.64f,Screen.height*.72f),"1층 안내도");float x=Screen.width*.28f,y=Screen.height*.27f,w=Screen.width*.44f,h=Screen.height*.42f;GUI.Box(new Rect(x,y,w,h),"로비");GUI.Box(new Rect(x,y,w*.22f,h*.34f),"경비실");GUI.Box(new Rect(x+w*.7f,y+h*.06f,w*.25f,h*.28f),"사무실");GUI.Box(new Rect(x+w*.7f,y+h*.62f,w*.25f,h*.28f),"비상계단");GUI.Label(new Rect(x+20,y+h+20,600,30),"E 또는 ESC: 지도 닫기",title);
    }

    GameObject Box(string n,Vector3 p,Vector3 s,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.isStatic=true;g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    void InvisibleBlock(string n,Vector3 p,Vector3 s){var g=Box(n,p,s,black);g.GetComponent<Renderer>().enabled=false;}
    GameObject Cylinder(string n,Vector3 p,Vector3 s,Material m,Quaternion q){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.isStatic=true;g.transform.position=p;g.transform.localScale=s;g.transform.rotation=q;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    void AddPoint(Vector3 p,Color c,float i,float range){var l=new GameObject("Point Light").AddComponent<Light>();l.type=LightType.Point;l.transform.position=p;l.color=c;l.intensity=i;l.range=range;l.shadows=LightShadows.None;}
}
