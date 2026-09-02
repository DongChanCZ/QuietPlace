using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class BlueprintFacilityBuilder
{
    const string RootName = "Blueprint Research Facility";
    static Transform root;
    static Material wallMat, floorMat, trimMat, darkMat, hazardMat, glassMat;

    [MenuItem("LabHorror/Build Blueprint Research Facility")]
    public static void Build()
    {
        DestroyNamed(RootName);
        DestroyNamed("Gameplay Research Facility");
        DestroyNamed("Research Facility Exterior Ground");

        root = new GameObject(RootName).transform;
        wallMat = Mat("BlueprintWall", new Color(.22f,.28f,.30f), 0.72f, 0.12f);
        floorMat = Mat("BlueprintFloor", new Color(.11f,.14f,.15f), 0.82f, 0.05f);
        trimMat = Mat("BlueprintCyan", new Color(.02f,.42f,.48f), 0.28f, 0.45f);
        darkMat = Mat("BlueprintDark", new Color(.025f,.035f,.04f), 0.75f, 0.2f);
        hazardMat = Mat("BlueprintHazard", new Color(.78f,.28f,.025f), 0.48f, 0.1f);
        glassMat = Mat("BlueprintWindow", new Color(.025f,.22f,.30f), 0.88f, 0.35f);

        BuildLevel(-4f, "B1");
        BuildLevel(0f, "1F");
        BuildLevel(4f, "2F");
        BuildLevel(8f, "3F");
        Box("Roof", new Vector3(0,11.55f,0), new Vector3(28,.3f,18), wallMat, root);
        BuildStairs();
        Decorate();
        BuildExterior();
        BuildFacadeDetails();
        foreach (var light in root.GetComponentsInChildren<Light>(true)) light.shadows = LightShadows.None;

        var player = GameObject.Find("Player");
        if (player) { player.transform.position = new Vector3(0,1.05f,-19); player.transform.rotation = Quaternion.identity; }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Blueprint Research Facility built from Unity primitives and project prefabs.");
    }

    static void BuildLevel(float y, string id)
    {
        var level = new GameObject(id + "_PLAN").transform; level.SetParent(root);
        Box(id+" Floor", new Vector3(0,y-.15f,0), new Vector3(28,.3f,18), floorMat, level);
        // Outer shell: front/south entrance, west escape, east stair connector.
        WallX(id+" North",-14,14,9,y,999,0,level);
        if (id == "1F") WallX(id+" South",-14,14,-9,y,0,2.4f,level);
        else WallX(id+" South",-14,14,-9,y,999,0,level);
        WallZ(id+" West",-9,9,-14,y,0,2.0f,level);
        WallZ(id+" East",-9,9,14,y,5.5f,2.2f,level);

        if (id == "1F") Build1F(y,level);
        else if (id == "2F") Build2F(y,level);
        else if (id == "3F") Build3F(y,level);
        else BuildB1(y,level);

        for (int x=-10;x<=10;x+=5) Box(id+" Corridor Light",new Vector3(x,y+3.25f,0),new Vector3(2.2f,.08f,.16f),trimMat,level,false);
    }

    static void Build1F(float y, Transform p)
    {
        // West: security/medical; center: large lobby; east: documents/warning route.
        WallZ("1F West Lobby Divider",-8.8f,2.0f,-5,y,-3.2f,1.9f,p);
        WallZ("1F West Upper Divider",2,8.8f,-5,y,5.3f,1.9f,p);
        WallX("1F West Room Split",-13.8f,-5,2,y,-9.2f,1.9f,p);
        WallZ("1F East Lobby Divider",-8.8f,8.8f,6.5f,y,-4.8f,1.9f,p);
        WallX("1F East Rooms",6.5f,13.8f,2.2f,y,10,1.9f,p);
        // U-shaped reception counter.
        Box("Lobby Desk Center",new Vector3(0,y+.65f,1.4f),new Vector3(5,.8f,1.3f),darkMat,p);
        Box("Lobby Desk Left",new Vector3(-2.1f,y+.65f,0),new Vector3(.8f,1.3f,3.5f),darkMat,p);
        Box("Lobby Desk Right",new Vector3(2.1f,y+.65f,0),new Vector3(.8f,1.3f,3.5f),darkMat,p);
    }

    static void Build2F(float y, Transform p)
    {
        // Long central chase corridor with three rooms on each side.
        WallX("2F Upper Rooms",-13.8f,13.8f,2,y,0,2,p);
        WallX("2F Lower Rooms",-13.8f,13.8f,-2,y,0,2,p);
        WallZ("2F Upper L",2,8.8f,-5,y,5.2f,1.8f,p);
        WallZ("2F Upper R",2,8.8f,5,y,5.2f,1.8f,p);
        WallZ("2F Lower L",-8.8f,-2,-5,y,-5.2f,1.8f,p);
        WallZ("2F Lower R",-8.8f,-2,5,y,-5.2f,1.8f,p);
    }

    static void Build3F(float y, Transform p)
    {
        // Development wraps a central server room; control/records/ventilation below.
        WallX("3F Server North",-5,5,3.8f,y,0,1.9f,p);
        WallX("3F Server South",-5,5,-1.8f,y,0,1.9f,p);
        WallZ("3F Server West",-1.8f,3.8f,-5,y,1,1.8f,p);
        WallZ("3F Server East",-1.8f,3.8f,5,y,1,1.8f,p);
        WallX("3F Lower Rooms",-13.8f,13.8f,-2.2f,y,-8,1.9f,p);
        WallZ("3F Lower Split L",-8.8f,-2.2f,-5,y,-5,1.8f,p);
        WallZ("3F Lower Split R",-8.8f,-2.2f,5,y,-5,1.8f,p);
    }

    static void BuildB1(float y, Transform p)
    {
        // Quarantine, waste, injection lab, secret bio lab and evolved egg chamber.
        WallX("B1 Upper Rooms",-13.8f,13.8f,2,y,-8,1.9f,p);
        WallZ("B1 Upper Split L",2,8.8f,-5,y,5.2f,1.8f,p);
        WallZ("B1 Upper Split R",2,8.8f,5,y,5.2f,1.8f,p);
        WallX("B1 Lower Rooms",-13.8f,13.8f,-2,y,0,2,p);
        WallZ("B1 Lower Split L",-8.8f,-2,-5,y,-5,1.8f,p);
        WallZ("B1 Egg Chamber",-8.8f,-2,7.5f,y,-5,1.8f,p);
    }

    static void BuildStairs()
    {
        var s=new GameObject("East Emergency Stairs").transform;s.SetParent(root);
        float[] bases={-4,0,4};
        foreach(float b in bases){
            Box("Landing",new Vector3(15.7f,b+.05f,5.5f),new Vector3(3.4f,.18f,2.2f),darkMat,s);
            Box("Mid Landing",new Vector3(15.7f,b+2.05f,-5.5f),new Vector3(3.4f,.18f,2.2f),darkMat,s);
            for(int i=0;i<10;i++){
                Box("Stair A",new Vector3(15.05f,b+.2f*(i+1),5-i*1.05f),new Vector3(1.25f,.22f,1.15f),darkMat,s);
                Box("Stair B",new Vector3(16.3f,b+2+.2f*(i+1),-5+i*1.05f),new Vector3(1.25f,.22f,1.15f),darkMat,s);
            }
        }
    }

    static void Decorate()
    {
        // Actual project prefabs: entrance/room doors, desks, PCs, server racks and lights.
        string door="Assets/SciFi Warehouse Kit/Prefabs/Structures/Walls/Wall Door.prefab";
        foreach(float y in new float[]{-4,0,4,8}){
            PlaceDoor(door,"STAIR DOOR",new Vector3(13.82f,y+1.7f,5.5f),Quaternion.identity,root);
            PlaceDoor(door,"WEST EXIT DOOR",new Vector3(-13.82f,y+1.7f,0),Quaternion.identity,root);
            foreach(float x in new float[]{-9,0,9}) PlaceDoor(door,"ROOM DOOR",new Vector3(x,y+1.7f,1.9f),Quaternion.Euler(0,90,0),root);
        }
        string table="Assets/ScifiOfficeLite/Prefabs/Tables/Table Set 1 Dark Oak.prefab";
        string chair="Assets/ScifiOfficeLite/Prefabs/Chairs and Stools/Office Chair.prefab";
        string pc="Assets/ScifiOfficeLite/Prefabs/Tech Accessories/PC 2.prefab";
        string server="Assets/ScifiOfficeLite/Prefabs/Tech Accessories/Server Rack.prefab";
        string light="Assets/ScifiOfficeLite/Prefabs/Lighting/Ceiling Lights/2D/Ceiling Light Bright.prefab";
        Vector3[] desks={new Vector3(-9,.02f,5.5f),new Vector3(0,.02f,5.5f),new Vector3(9,.02f,5.5f),new Vector3(-9,4.02f,5.5f),new Vector3(0,4.02f,5.5f),new Vector3(9,8.02f,-5.5f)};
        foreach(var pos in desks){PlacePrefab(table,"Lab Table",pos,Quaternion.identity,Vector3.one,root);PlacePrefab(chair,"Office Chair",pos+new Vector3(0,0,-1.4f),Quaternion.identity,Vector3.one,root);PlacePrefab(pc,"Computer",pos+new Vector3(0,1,0),Quaternion.identity,Vector3.one,root);}
        for(int i=-2;i<=2;i++) PlacePrefab(server,"Server Rack",new Vector3(i*1.45f,8.02f,1),Quaternion.identity,Vector3.one,root);
        foreach(float y in new float[]{0,4,8}) foreach(float x in new float[]{-9,0,9}) PlacePrefab(light,"Ceiling Light",new Vector3(x,y+3.15f,0),Quaternion.identity,Vector3.one,root);
        // B1 quarantine pods and evolved specimen focal point.
        for(int i=0;i<3;i++) Capsule("Quarantine Pod",new Vector3(-10+i*2.4f,-2.85f,5.7f),new Vector3(1.3f,2.3f,1.3f),trimMat,root);
        Capsule("Evolved Egg Chamber",new Vector3(10,-2.3f,-5.5f),new Vector3(2.5f,3.2f,2.5f),hazardMat,root);
    }

    static void BuildExterior()
    {
        Box("Exterior Ground",new Vector3(0,-.35f,0),new Vector3(52,.5f,52),floorMat,root);
        Box("Entrance Canopy",new Vector3(0,3.1f,-10.4f),new Vector3(6,.25f,3),wallMat,root);
        Box("Entrance Sign",new Vector3(0,3.7f,-9.15f),new Vector3(5,.65f,.15f),trimMat,root,false);
        var sun=GameObject.Find("Moon Light"); if(sun) sun.transform.rotation=Quaternion.Euler(38,-25,0);
    }

    static void BuildFacadeDetails()
    {
        var facade=new GameObject("VISIBLE DOORS AND WINDOWS").transform;facade.SetParent(root);
        // Guaranteed-visible main doorway. The decorative leaves have no collider so gameplay entry remains open.
        Box("Main Door Frame Left",new Vector3(-1.35f,1.65f,-9.16f),new Vector3(.28f,3.3f,.32f),trimMat,facade);
        Box("Main Door Frame Right",new Vector3(1.35f,1.65f,-9.16f),new Vector3(.28f,3.3f,.32f),trimMat,facade);
        Box("Main Door Frame Top",new Vector3(0,3.25f,-9.16f),new Vector3(2.98f,.28f,.32f),trimMat,facade);
        Box("Main Door Left Leaf",new Vector3(-.68f,1.55f,-9.19f),new Vector3(1.22f,2.85f,.12f),darkMat,facade,false);
        Box("Main Door Right Leaf",new Vector3(.68f,1.55f,-9.19f),new Vector3(1.22f,2.85f,.12f),darkMat,facade,false);
        Box("Main Door Glass L",new Vector3(-.68f,2.05f,-9.27f),new Vector3(.72f,1.25f,.035f),glassMat,facade,false);
        Box("Main Door Glass R",new Vector3(.68f,2.05f,-9.27f),new Vector3(.72f,1.25f,.035f),glassMat,facade,false);
        Box("Main Door Handle L",new Vector3(-.12f,1.45f,-9.32f),new Vector3(.08f,.55f,.08f),hazardMat,facade,false);
        Box("Main Door Handle R",new Vector3(.12f,1.45f,-9.32f),new Vector3(.08f,.55f,.08f),hazardMat,facade,false);

        // Front windows follow the blueprint facade rhythm on all occupied floors.
        foreach(float level in new float[]{0,4,8})
            foreach(float x in new float[]{-10,-6,6,10}) Window("Front Window",new Vector3(x,level+2.05f,-9.15f),facade);
    }

    static void Window(string n,Vector3 c,Transform p)
    {
        Box(n+" Glass",c,new Vector3(2.25f,1.35f,.06f),glassMat,p,false);
        Box(n+" Top",c+new Vector3(0,.77f,-.02f),new Vector3(2.55f,.16f,.12f),trimMat,p,false);
        Box(n+" Bottom",c+new Vector3(0,-.77f,-.02f),new Vector3(2.55f,.16f,.12f),trimMat,p,false);
        Box(n+" Left",c+new Vector3(-1.2f,0,-.02f),new Vector3(.16f,1.7f,.12f),trimMat,p,false);
        Box(n+" Right",c+new Vector3(1.2f,0,-.02f),new Vector3(.16f,1.7f,.12f),trimMat,p,false);
        Box(n+" Mullion",c+new Vector3(0,0,-.03f),new Vector3(.10f,1.35f,.10f),trimMat,p,false);
    }

    static GameObject Box(string n,Vector3 pos,Vector3 size,Material mat,Transform parent,bool collider=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(parent);g.transform.position=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;g.isStatic=true;
        if(!collider) Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static GameObject Capsule(string n,Vector3 pos,Vector3 size,Material mat,Transform parent){var g=GameObject.CreatePrimitive(PrimitiveType.Capsule);g.name=n;g.transform.SetParent(parent);g.transform.position=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;g.isStatic=true;return g;}
    static void WallX(string n,float a,float b,float z,float y,float door,float width,Transform p){if(width<=0||door>100){Box(n,new Vector3((a+b)/2,y+1.95f,z),new Vector3(b-a,3.9f,.24f),wallMat,p);return;}float l=door-width/2,r=door+width/2;if(l>a)Box(n+" L",new Vector3((a+l)/2,y+1.95f,z),new Vector3(l-a,3.9f,.24f),wallMat,p);if(r<b)Box(n+" R",new Vector3((r+b)/2,y+1.95f,z),new Vector3(b-r,3.9f,.24f),wallMat,p);Box(n+" Header",new Vector3(door,y+3.45f,z),new Vector3(width,.9f,.24f),wallMat,p);}
    static void WallZ(string n,float a,float b,float x,float y,float door,float width,Transform p){if(width<=0||door>100){Box(n,new Vector3(x,y+1.95f,(a+b)/2),new Vector3(.24f,3.9f,b-a),wallMat,p);return;}float l=door-width/2,r=door+width/2;if(l>a)Box(n+" A",new Vector3(x,y+1.95f,(a+l)/2),new Vector3(.24f,3.9f,l-a),wallMat,p);if(r<b)Box(n+" B",new Vector3(x,y+1.95f,(r+b)/2),new Vector3(.24f,3.9f,b-r),wallMat,p);Box(n+" Header",new Vector3(x,y+3.45f,door),new Vector3(.24f,.9f,width),wallMat,p);}
    static GameObject PlacePrefab(string path,string n,Vector3 pos,Quaternion rot,Vector3 scale,Transform p){var a=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!a)return null;var g=(GameObject)PrefabUtility.InstantiatePrefab(a);g.name=n;g.transform.SetParent(p);g.transform.position=pos;g.transform.rotation=rot;g.transform.localScale=scale;return g;}
    static GameObject PlaceDoor(string path,string n,Vector3 center,Quaternion rot,Transform p){var g=PlacePrefab(path,n,Vector3.zero,rot,Vector3.one*.34f,p);if(!g)return null;var rs=g.GetComponentsInChildren<Renderer>(true);if(rs.Length==0){g.transform.position=center;return g;}var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);g.transform.position+=center-b.center;return g;}
    static GameObject PlaceSized(string path,string n,Vector3 center,Quaternion rot,Vector3 desired,Transform p){var g=PlacePrefab(path,n,Vector3.zero,rot,Vector3.one,p);if(!g)return null;var rs=g.GetComponentsInChildren<Renderer>(true);if(rs.Length==0){g.transform.position=center;return g;}var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);g.transform.localScale=new Vector3(desired.x/Mathf.Max(b.size.x,.001f),desired.y/Mathf.Max(b.size.y,.001f),desired.z/Mathf.Max(b.size.z,.001f));b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);g.transform.position+=center-b.center;return g;}
    static Material Mat(string n,Color c,float smooth,float metal){string path="Assets/ResearchFacility/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){var sh=Shader.Find("Universal Render Pipeline/Lit");if(!sh)sh=Shader.Find("Standard");m=new Material(sh);AssetDatabase.CreateAsset(m,path);}m.color=c;if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal);EditorUtility.SetDirty(m);return m;}
    static void DestroyNamed(string n){var g=GameObject.Find(n);if(g)Object.DestroyImmediate(g);}
}
