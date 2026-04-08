using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct AlchemyRecipe
{
    public string recipeName;
    public List<string> requiredInputs;
    public GameObject outputPrefab;
    public float processTime;
}

[CreateAssetMenu(fileName = "NewRecipeDatabase", menuName = "Alchemy/Recipe Database")]
public class RecipeDatabaseSO : ScriptableObject
{
    public List<AlchemyRecipe> allRecipes;
}