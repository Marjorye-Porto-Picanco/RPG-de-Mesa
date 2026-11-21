using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPGdeMesa.Domain.Magias;

public interface IMagiaRepository
{
    Task<List<Magia>> ListarTodasAsync();
}