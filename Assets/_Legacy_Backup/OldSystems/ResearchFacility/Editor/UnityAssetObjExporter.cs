using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class UnityAssetObjExporter
{
    [MenuItem("LabHorror/Export Building Assets For Blender")]
    public static void ExportAll()
    {
        var assets = new Dictionary<string,string>
        {
            {"MainDoor", "Assets/Abandoned_Asylum/Models/MainDoor.fbx"},
            {"Wall", "Assets/ScifiOfficeLite/Meshes/Walls/Wall 2.FBX"},
            {"WindowWall", "Assets/ScifiOfficeLite/Meshes/Walls/Wall With Windows Set 10.FBX"},
            {"SciFiDoor", "Assets/ScifiOfficeLite/Meshes/Sci-Fi Environment/SciFiDoor.FBX"},
            {"ServerRack", "Assets/ScifiOfficeLite/Meshes/Sci-Fi Environment/Server Rack.FBX"},
            {"PC", "Assets/ScifiOfficeLite/Meshes/Office Interior/Computers/PC 2.FBX"},
            {"TableSet", "Assets/ScifiOfficeLite/Meshes/Office Interior/Tables/Table Set 1.FBX"}
        };
        string folder="Assets/ResearchFacility/BlenderSource";
        Directory.CreateDirectory(folder);
        int exported=0;
        foreach(var pair in assets)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
            if(asset==null){Debug.LogWarning("Missing model: "+pair.Value);continue;}
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            instance.transform.localScale=Vector3.one;
            ExportObject(instance,folder+"/"+pair.Key+".obj",folder+"/"+pair.Key+".mtl");
            Object.DestroyImmediate(instance);exported++;
        }
        AssetDatabase.Refresh();
        Debug.Log("Exported "+exported+" Unity model assets for Blender.");
    }

    static void ExportObject(GameObject root,string objPath,string mtlPath)
    {
        var obj=new StringBuilder();var mtl=new StringBuilder();
        obj.AppendLine("mtllib "+Path.GetFileName(mtlPath));
        int vertexOffset=1,uvOffset=1,normalOffset=1;
        var written=new HashSet<string>();
        foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=mf.sharedMesh;if(mesh==null)continue;
            var tr=mf.transform;obj.AppendLine("o "+Safe(mf.name));
            foreach(var v in mesh.vertices){var p=tr.TransformPoint(v);obj.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"v {0} {1} {2}",p.x,p.y,p.z));}
            foreach(var uv in mesh.uv)obj.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"vt {0} {1}",uv.x,uv.y));
            foreach(var n in mesh.normals){var p=tr.TransformDirection(n).normalized;obj.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"vn {0} {1} {2}",p.x,p.y,p.z));}
            var renderer=mf.GetComponent<Renderer>();
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                Material mat=renderer&&sub<renderer.sharedMaterials.Length?renderer.sharedMaterials[sub]:null;
                string mn=mat?Safe(mat.name):"Default";obj.AppendLine("usemtl "+mn);
                if(!written.Contains(mn))
                {
                    written.Add(mn);Color c=mat?mat.color:Color.gray;
                    mtl.AppendLine("newmtl "+mn);mtl.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Kd {0} {1} {2}",c.r,c.g,c.b));mtl.AppendLine("Ns 32");
                }
                var tri=mesh.GetTriangles(sub);
                for(int i=0;i<tri.Length;i+=3)
                {
                    obj.Append("f");
                    for(int k=2;k>=0;k--){int idx=tri[i+k];int vi=vertexOffset+idx;int ti=mesh.uv.Length==mesh.vertexCount?uvOffset+idx:vi;int ni=mesh.normals.Length==mesh.vertexCount?normalOffset+idx:vi;obj.Append(" "+vi+"/"+ti+"/"+ni);}obj.AppendLine();
                }
            }
            vertexOffset+=mesh.vertexCount;uvOffset+=mesh.uv.Length;normalOffset+=mesh.normals.Length;
        }
        File.WriteAllText(objPath,obj.ToString());File.WriteAllText(mtlPath,mtl.ToString());
    }
    static string Safe(string s){return s.Replace(' ','_').Replace('/','_');}
}
