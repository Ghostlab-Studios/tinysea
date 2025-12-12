using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RunSpeciesList", menuName = "TinySea/RunSpeciesList")]
public class RunSpeciesList : ScriptableObject
{
    [Header("OrginalDataBase")]
    public SpeciesDatabase SpeciesDatabase;

    [Header("Header")]
    public List<SpeciesData> speciesList = new List<SpeciesData>();

}