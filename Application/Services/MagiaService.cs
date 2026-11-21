using RPGdeMesa.Domain.Magias;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPGdeMesa.Application.Services;

public class MagiaService
{
    private readonly IMagiaRepository _magiaRepository;

    public MagiaService(IMagiaRepository magiaRepository)
    {
        _magiaRepository = magiaRepository;
    }

    public Task<List<Magia>> ObterTodasMagiasAsync() =>
        _magiaRepository.ListarTodasAsync();
}