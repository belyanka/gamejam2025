using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueController : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text dialogueText;       // текстовое поле
    public Button nextButton;           // кнопка "Далее"// изображение персонажа

    [Header("Реплики")]
    [TextArea(2, 5)]
    public string[] lines;              // массив реплик

    private int currentIndex = 0;       // индекс текущей реплики

    void Start()
    {
        // Устанавливаем первую реплику
        ShowLine();

        // Подписываем кнопку на метод NextLine
        nextButton.onClick.AddListener(NextLine);
    }

    void ShowLine()
    {
        if (currentIndex < lines.Length)
        {
            dialogueText.text = lines[currentIndex];
        }
        else
        {
            EndDialogue();
        }
    }

    public void NextLine()
    {
        currentIndex++;

        if (currentIndex < lines.Length)
        {
            ShowLine();
        }
        else
        {
            EndDialogue();
        }
    }

    void EndDialogue()
    {
        GameManager.Instance.StartGame();
    }
}