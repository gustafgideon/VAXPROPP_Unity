using UnityEngine;
using FMOD.Studio;
using FMODUnity;

[CreateAssetMenu(fileName = "PlayerAudio", menuName = "Scriptable Objects/PlayerAudio")]
public class PlayerAudio : ScriptableObject
{
    [SerializeField] private EventReference playerWalk, playerRun, playerJump, playerLand, playerWalkStair;

    
    
    
    
    public void PlayerWalkAudio(GameObject walkObj, string surface, float stairDirection = 0f)
    {

        EventInstance playerWalkInstance = RuntimeManager.CreateInstance(playerWalk);
        RuntimeManager.AttachInstanceToGameObject(playerWalkInstance, walkObj.transform);
        
        switch (surface) //vill jämföra innehållet i "surface"parametern
        {
            //Namn på olika taggar med olika numeriska värden och  refererar till parametern i FMOD "Surface"
            case "Grass":
                playerWalkInstance.setParameterByName("Surface", 0f); 
                break;
            case "Dirt":
                playerWalkInstance.setParameterByName("Surface", 1f);
                break;
            case "Wood":
                playerWalkInstance.setParameterByName("Surface", 2f);
                break;
            case "Concrete":
                playerWalkInstance.setParameterByName("Surface", 3f);
                break;
            case "Sand":
                playerWalkInstance.setParameterByName("Surface", 4f);
                break;
            case "Stone":
                playerWalkInstance.setParameterByName("Surface", 5f);
                break;
            case "Concrete_Stair":
                playerWalkInstance.setParameterByName("Surface", 6f);
                break;
            case "Player":
                break;
            default:
                playerWalkInstance.setParameterByName("Surface", 0f);
                break;
        }
        
        // StairDirection parameter
        playerWalkInstance.setParameterByName("StairDirection", stairDirection);

        
        playerWalkInstance.start();
        playerWalkInstance.release();
    }
}
