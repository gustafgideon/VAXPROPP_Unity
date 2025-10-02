using UnityEngine;
using FMOD.Studio;
using FMODUnity;

[CreateAssetMenu(fileName = "PlayerAudio", menuName = "Scriptable Objects/PlayerAudio")]
public class PlayerAudio : ScriptableObject
{
    [SerializeField] private EventReference playerWalk, playerRun, playerJump, playerLand;

    
    
    
    
    public void PlayerWalkAudio(GameObject walkObj, string surface)
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
            case "Gravel":
                playerWalkInstance.setParameterByName("Surface", 3f);
                break;
            case "Concrete":
                playerWalkInstance.setParameterByName("Surface", 4f);
                break;
            case "Player":
                break;
            default:
                playerWalkInstance.setParameterByName("Surface", 0f);
                break;
        }
        playerWalkInstance.start();
        playerWalkInstance.release();
    }
    
    public void PlayerRunAudio(GameObject runObj, string surface)
    {

        EventInstance playerRunInstance = RuntimeManager.CreateInstance(playerRun);
        RuntimeManager.AttachInstanceToGameObject(playerRunInstance, runObj.transform);
        
        switch (surface) //vill jämföra innehållet i "surface"parametern
        {
            //Namn på olika taggar med olika numeriska värden och  refererar till parametern i FMOD "Surface"
            case "Concrete":
                playerRunInstance.setParameterByName("Surface", 0f); 
                break;
            case "Metal":
                playerRunInstance.setParameterByName("Surface", 1f);
                break;
            case "Wood":
                playerRunInstance.setParameterByName("Surface", 2f);
                break;
            case "Gravel":
                playerRunInstance.setParameterByName("Surface", 3f);
                break;
            case "Grass":
                playerRunInstance.setParameterByName("Surface", 4f);
                break;
            case "Player":
                break;
            default:
                playerRunInstance.setParameterByName("Surface", 0f);
                break;
        }
        playerRunInstance.start();
        playerRunInstance.release();
    }
}
