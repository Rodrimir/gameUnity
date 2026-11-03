namespace GameYunit.Spells
{
    /// <summary>
    /// O que a interface precisa saber de um conjurador.
    /// </summary>
    /// <remarks>
    /// O conjurador concreto vive em <c>KitBridge</c>, no Assembly-CSharp, porque usa tipos do 3D Game Kit;
    /// a UI vive na asmdef <c>GameYunit.Runtime</c>, que não enxerga o Assembly-CSharp. Esta interface é a
    /// ponte no sentido permitido: o Assembly-CSharp referencia as asmdefs, não o contrário.
    /// </remarks>
    public interface ISpellCaster
    {
        /// <summary>Magia equipada, ou nulo quando não há catálogo ou escolha válida.</summary>
        SpellDefinition CurrentSpell { get; }

        /// <summary>Fração da recarga cumprida, de 0 (acabou de conjurar) a 1 (pronta).</summary>
        float CooldownProgress { get; }
    }
}
